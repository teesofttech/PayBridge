using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PayBridge.SDK;

/// <summary>
/// KNET gateway — Kuwait's national payment network (KWD currency).
/// Docs: https://kpaygateway.net/docs
/// </summary>
public class KnetGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KnetGateway> _logger;
    private const string BaseUrl = "https://kpaygateway.net";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Knet;

    public KnetGateway(IOptions<PaymentGatewayConfig> config, ILogger<KnetGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Knet.TransportId))
            throw new InvalidOperationException("KNET TransportId is required");

        if (string.IsNullOrEmpty(_config.Knet.Password))
            throw new InvalidOperationException("KNET Password is required");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating KNET payment for customer {Email}", request.CustomerEmail);

        try
        {
            var txRef = $"KN_{Guid.NewGuid():N}";

            var knetRequest = new
            {
                tran_id = txRef,
                amt = request.Amount.ToString("F3"),
                currency = "414", // ISO 4217 numeric code for KWD
                action = "1",     // 1 = purchase
                langid = "USA",
                responseURL = request.RedirectUrl,
                errorURL = request.RedirectUrl,
                trackid = txRef,
                udf1 = request.CustomerEmail,
                udf2 = request.CustomerName,
                udf3 = request.CustomerPhone
            };

            var content = new StringContent(JsonSerializer.Serialize(knetRequest), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Add("tranportalId", _config.Knet.TransportId);
            _httpClient.DefaultRequestHeaders.Add("password", _config.Knet.Password);

            var response = await _httpClient.PostAsync("/api/PaymentInit", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("KNET response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("paymentId", out var paymentId))
            {
                var checkoutUrl = $"{BaseUrl}/api/PaymentPage?paymentId={paymentId.GetString()}";
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "KNET payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = checkoutUrl,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "payment_id", paymentId.GetString() ?? string.Empty }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("error", out var error)
                ? error.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("KNET payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating KNET payment");
            throw new Exception("Failed to create payment with KNET", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying KNET payment: {Reference}", transactionReference);

        try
        {
            var response = await _httpClient.GetAsync($"/api/PaymentStatus?trackid={transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("KNET verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                var result = root.TryGetProperty("result", out var resultProp)
                    ? resultProp.GetString()?.ToUpper() ?? string.Empty
                    : string.Empty;

                var paymentStatus = result switch
                {
                    "CAPTURED" or "APPROVED" => PaymentStatus.Successful,
                    "DECLINED" or "FAILED" or "NOT CAPTURED" => PaymentStatus.Failed,
                    "CANCELLED" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = root.TryGetProperty("amt", out var amt)
                        ? decimal.Parse(amt.GetString() ?? "0")
                        : 0,
                    Currency = "KWD",
                    PaymentDate = DateTime.UtcNow
                };
            }

            return new VerificationResponse
            {
                Success = false,
                TransactionReference = transactionReference,
                Message = "Verification request failed",
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying KNET payment");
            throw new Exception("Failed to verify payment with KNET", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing KNET refund for: {Reference}", request.TransactionReference);

        try
        {
            var refundRequest = new
            {
                trackid = request.TransactionReference,
                amt = request.Amount.ToString("F3"),
                action = "2" // 2 = refund
            };

            var content = new StringContent(JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/Refund", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("KNET refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            var result = root.TryGetProperty("result", out var resultProp)
                ? resultProp.GetString()?.ToUpper() ?? string.Empty
                : string.Empty;

            if (response.IsSuccessStatusCode && result == "CAPTURED")
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = root.TryGetProperty("ref", out var refId) ? refId.GetString() ?? string.Empty : string.Empty,
                    TransactionReference = request.TransactionReference,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund processed successfully",
                    RefundDate = DateTime.UtcNow
                };
            }

            return new RefundResponse
            {
                Success = false,
                Message = root.TryGetProperty("error", out var error) ? error.GetString() ?? "Refund failed" : "Refund request failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing KNET refund");
            throw new Exception("Failed to process refund with KNET", ex);
        }
    }
}
