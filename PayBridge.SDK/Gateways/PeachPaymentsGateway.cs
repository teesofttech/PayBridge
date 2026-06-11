using System.Net.Http.Headers;
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
/// Peach Payments gateway (South Africa, Kenya, Nigeria, Botswana).
/// Currencies: ZAR, KES, NGN, BWP, USD.
/// Auth: Bearer AccessToken + EntityId.
/// Docs: https://developer.peachpayments.com
/// </summary>
public class PeachPaymentsGateway : IPaymentGateway
{
    private const string LiveBaseUrl = "https://eu-prod.oppwa.com/v1";
    private const string SandboxBaseUrl = "https://eu-test.oppwa.com/v1";
    private const string TxRefPrefix = "PEACH_";

    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PeachPaymentsGateway> _logger;

    public PeachPaymentsGateway(
        PaymentGatewayConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<PeachPaymentsGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClientFactory.CreateClient(nameof(PeachPaymentsGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.PeachPayments;

    private string BaseUrl => _config.PeachPayments.IsSandbox ? SandboxBaseUrl : LiveBaseUrl;

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.PeachPayments.AccessToken);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var txRef = $"{TxRefPrefix}{Guid.NewGuid():N}";

            var formData = new Dictionary<string, string>
            {
                ["entityId"]              = _config.PeachPayments.EntityId,
                ["amount"]                = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ["currency"]              = request.Currency ?? "ZAR",
                ["paymentType"]           = "DB",
                ["merchantTransactionId"] = txRef,
                ["customer.email"]        = request.CustomerEmail ?? string.Empty,
                ["customer.givenName"]    = request.CustomerName?.Split(' ').FirstOrDefault() ?? string.Empty,
                ["customer.surname"]      = request.CustomerName?.Split(' ').LastOrDefault() ?? string.Empty,
                ["billing.country"]       = CurrencyToCountry(request.Currency ?? "ZAR"),
            };

            SetAuthHeader();
            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync($"{BaseUrl}/checkouts", content);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PeachPayments checkout response: {Body}", body);

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"PeachPayments checkout failed: {response.StatusCode}",
                    Status = PaymentStatus.Failed
                };
            }

            var json = JsonDocument.Parse(body).RootElement;
            var resultCode = json.TryGetProperty("result", out var resultEl) &&
                             resultEl.TryGetProperty("code", out var codeEl)
                ? codeEl.GetString() : null;

            var checkoutId = json.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            bool success = resultCode != null && resultCode.StartsWith("000");

            var paymentUrl = checkoutId != null
                ? $"{BaseUrl}/paymentWidgets.js?checkoutId={checkoutId}"
                : null;

            return new PaymentResponse
            {
                Success = success,
                TransactionReference = txRef,
                CheckoutUrl = paymentUrl ?? string.Empty,
                Message = resultEl.TryGetProperty("description", out var descEl)
                    ? descEl.GetString() ?? "Unknown" : "Unknown",
                Status = success ? PaymentStatus.Pending : PaymentStatus.Failed,
                GatewayResponse = new Dictionary<string, string>
                {
                    ["checkoutId"] = checkoutId ?? string.Empty,
                    ["resultCode"] = resultCode ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating PeachPayments checkout");
            throw new PaymentGatewayException("Failed to initiate PeachPayments payment", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        try
        {
            var url = $"{BaseUrl}/payments?entityId={Uri.EscapeDataString(_config.PeachPayments.EntityId)}" +
                      $"&merchantTransactionId={Uri.EscapeDataString(transactionReference)}";

            SetAuthHeader();
            var response = await _httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PeachPayments verify response: {Body}", body);

            if (!response.IsSuccessStatusCode)
            {
                return new VerificationResponse
                {
                    Success = false,
                    TransactionReference = transactionReference,
                    Status = PaymentStatus.Failed,
                    Message = $"PeachPayments verification failed: {response.StatusCode}"
                };
            }

            var json = JsonDocument.Parse(body).RootElement;
            var payment = json.ValueKind == JsonValueKind.Array ? json[0] : json;

            var resultCode = payment.TryGetProperty("result", out var resultEl) &&
                             resultEl.TryGetProperty("code", out var codeEl)
                ? codeEl.GetString() : null;

            bool success = resultCode != null &&
                           (resultCode.StartsWith("000.0") || resultCode.StartsWith("000.100"));

            decimal.TryParse(
                payment.TryGetProperty("amount", out var amtEl) ? amtEl.GetString() : "0",
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount);

            var currency = payment.TryGetProperty("currency", out var curEl)
                ? curEl.GetString() ?? string.Empty : string.Empty;

            var paymentBrand = payment.TryGetProperty("paymentBrand", out var pbEl)
                ? pbEl.GetString() ?? "Card" : "Card";

            return new VerificationResponse
            {
                Success = success,
                TransactionReference = transactionReference,
                Amount = amount,
                Currency = currency,
                Status = success ? PaymentStatus.Successful : PaymentStatus.Failed,
                Message = resultEl.TryGetProperty("description", out var descEl)
                    ? descEl.GetString() ?? "Unknown" : "Unknown",
                PaymentMethod = paymentBrand,
                GatewayResponse = new Dictionary<string, string>
                {
                    ["resultCode"] = resultCode ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PeachPayments payment: {Ref}", transactionReference);
            throw new PaymentGatewayException("Failed to verify PeachPayments payment", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["entityId"]    = _config.PeachPayments.EntityId,
                ["amount"]      = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ["currency"]    = "ZAR",
                ["paymentType"] = "RF",
            };

            SetAuthHeader();
            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync($"{BaseUrl}/payments/{request.TransactionReference}", content);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("PeachPayments refund response: {Body}", body);

            var json = JsonDocument.Parse(body).RootElement;
            var resultCode = json.TryGetProperty("result", out var resultEl) &&
                             resultEl.TryGetProperty("code", out var codeEl)
                ? codeEl.GetString() : null;

            bool success = resultCode != null && resultCode.StartsWith("000");
            var refundId = json.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;

            return new RefundResponse
            {
                Success = success,
                RefundReference = refundId,
                TransactionReference = request.TransactionReference,
                Amount = request.Amount,
                Status = success ? PaymentStatus.Refunded : PaymentStatus.Failed,
                Message = resultEl.TryGetProperty("description", out var descEl)
                    ? descEl.GetString() ?? "Unknown" : "Unknown"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PeachPayments refund: {Ref}", request.TransactionReference);
            throw new PaymentGatewayException("Failed to process PeachPayments refund", ex);
        }
    }

    private static string CurrencyToCountry(string currency) => currency.ToUpperInvariant() switch
    {
        "ZAR" => "ZA",
        "KES" => "KE",
        "NGN" => "NG",
        "BWP" => "BW",
        _     => "ZA"
    };
}
