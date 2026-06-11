using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Exceptions;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK;

/// <summary>
/// PawaPay payment gateway implementation (Africa - mobile money).
/// Supports MTN MoMo, Airtel Money, M-Pesa, Orange Money, Tigo Pesa, Vodacom and more
/// across Ghana, Tanzania, Uganda, Rwanda, Zambia, DRC, Cameroon, Ivory Coast and others.
/// </summary>
public class PawaPayGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PawaPayGateway> _logger;

    private const string LiveBaseUrl = "https://api.pawapay.io";
    private const string SandboxBaseUrl = "https://api.sandbox.pawapay.io";
    private const string TxRefPrefix = "PP_";

    public PawaPayGateway(
        PaymentGatewayConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<PawaPayGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClientFactory.CreateClient(nameof(PawaPayGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.PawaPay;

    private string BaseUrl => _config.PawaPay.IsSandbox ? SandboxBaseUrl : LiveBaseUrl;

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.PawaPay.ApiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var depositId = $"{TxRefPrefix}{Guid.NewGuid():N}";
            var amount = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var currency = request.Currency ?? "GHS";

            // PawaPay requires a correspondent (mobile money provider code) and phone number.
            // Phone number is expected in request.CustomerPhone in international format (e.g. 233XXXXXXXXX)
            var phone = request.CustomerPhone ?? string.Empty;

            // Derive correspondent from currency — default to common providers per currency
            var correspondent = DeriveCorrespondent(currency, request.Metadata);

            var payload = new
            {
                depositId,
                amount,
                currency,
                correspondent,
                payer = new
                {
                    type = "MSISDN",
                    address = new { value = phone }
                },
                customerTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                statementDescription = (request.Description ?? "Payment").Length > 22
                    ? (request.Description ?? "Payment")[..22]
                    : (request.Description ?? "Payment"),
                metadata = new[]
                {
                    new { fieldName = "orderId", fieldValue = depositId, isPII = false }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            SetAuthHeader();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/deposits", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PawaPay deposit response: {Body}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"PawaPay payment initiation failed: {response.StatusCode}",
                    Status = PaymentStatus.Failed
                };
            }

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

            // PawaPay deposit statuses: ACCEPTED, DUPLICATE_IGNORED, REJECTED
            if (status == "REJECTED")
            {
                var rejectionCode = root.TryGetProperty("rejectionReason", out var rr) &&
                                    rr.TryGetProperty("rejectionCode", out var rc) ? rc.GetString() : "UNKNOWN";
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"PawaPay deposit rejected: {rejectionCode}",
                    Status = PaymentStatus.Failed
                };
            }

            return new PaymentResponse
            {
                Success = true,
                TransactionReference = depositId,
                Message = "Mobile money payment initiated. Customer will receive a prompt.",
                Status = PaymentStatus.Pending,
                GatewayResponse = new Dictionary<string, string>
                {
                    ["depositId"] = depositId,
                    ["status"] = status ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating PawaPay deposit");
            throw new PaymentGatewayException("Failed to initiate PawaPay payment", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"{BaseUrl}/deposits/{transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PawaPay verify response: {Body}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                return new VerificationResponse
                {
                    Success = false,
                    TransactionReference = transactionReference,
                    Status = PaymentStatus.Failed,
                    Message = $"PawaPay verification failed: {response.StatusCode}"
                };
            }

            // Response is an array with one element
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement[0]
                : doc.RootElement;

            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var amountStr = root.TryGetProperty("amount", out var am) ? am.GetString() : "0";
            var currency = root.TryGetProperty("currency", out var cur) ? cur.GetString() : string.Empty;

            decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount);

            // PawaPay statuses: COMPLETED, FAILED, TIMED_OUT, ENQUEUED
            var paymentStatus = status == "COMPLETED" ? PaymentStatus.Successful
                : status is "FAILED" or "TIMED_OUT" ? PaymentStatus.Failed
                : PaymentStatus.Pending;

            return new VerificationResponse
            {
                Success = status == "COMPLETED",
                TransactionReference = transactionReference,
                Amount = amount,
                Currency = currency ?? string.Empty,
                Status = paymentStatus,
                Message = status == "COMPLETED" ? "Payment completed successfully" : $"Payment status: {status}",
                GatewayResponse = new Dictionary<string, string> { ["status"] = status ?? string.Empty }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PawaPay payment: {Ref}", transactionReference);
            throw new PaymentGatewayException("Failed to verify PawaPay payment", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        try
        {
            var refundId = $"{TxRefPrefix}R_{Guid.NewGuid():N}";
            var amount = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            var payload = new
            {
                refundId,
                depositId = request.TransactionReference,
                amount,
                metadata = new[]
                {
                    new { fieldName = "reason", fieldValue = request.Reason ?? "Customer request", isPII = false }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            SetAuthHeader();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/refunds", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PawaPay refund response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var isSuccess = response.IsSuccessStatusCode && status != "REJECTED";

            return new RefundResponse
            {
                Success = isSuccess,
                RefundReference = refundId,
                TransactionReference = request.TransactionReference,
                Amount = request.Amount,
                Message = isSuccess ? "Refund initiated successfully" : $"Refund failed: {status}",
                Status = isSuccess ? PaymentStatus.Refunded : PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PawaPay refund: {Ref}", request.TransactionReference);
            throw new PaymentGatewayException("Failed to process PawaPay refund", ex);
        }
    }

    /// <summary>
    /// Derives the PawaPay correspondent code from the currency.
    /// Can be overridden via request metadata key "pawapay_correspondent".
    /// </summary>
    private static string DeriveCorrespondent(string currency, Dictionary<string, string>? metadata)
    {
        if (metadata != null && metadata.TryGetValue("pawapay_correspondent", out var custom))
            return custom;

        return currency.ToUpper() switch
        {
            "GHS" => "MTN_MOMO_GHA",
            "TZS" => "VODACOM_TZA",
            "UGX" => "MTN_MOMO_UGA",
            "RWF" => "MTN_MOMO_RWA",
            "ZMW" => "MTN_MOMO_ZMB",
            "CDF" => "AIRTEL_DRC",
            "XOF" => "ORANGE_CIV",   // Ivory Coast
            "XAF" => "MTN_MOMO_CMR", // Cameroon
            "MWK" => "AIRTEL_MWI",   // Malawi
            _     => "MTN_MOMO_GHA"  // default fallback
        };
    }
}
