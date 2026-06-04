using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Constants;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PayBridge.SDK;

public class KorapayGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KorapayGateway> _logger;
    private const string BaseUrl = "https://api.korapay.com/merchant/api/v1";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Korapay;

    public KorapayGateway(IOptions<PaymentGatewayConfig> config, ILogger<KorapayGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_config.Korapay.SecretKey))
        {
            throw new InvalidOperationException("Korapay secret key is required");
        }

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Korapay.SecretKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Korapay payment for customer {Email}", SanitizeForLog(request.CustomerEmail));

        var txRef = $"{GatewayReferencePrefixes.Korapay}{Guid.NewGuid():N}";
        var payload = new
        {
            amount = request.Amount,
            currency = request.Currency.ToUpperInvariant(),
            reference = txRef,
            redirect_url = request.RedirectUrl,
            notification_url = request.WebhookUrl,
            customer = new
            {
                name = request.CustomerName,
                email = request.CustomerEmail
            },
            narration = request.Description,
            metadata = request.Metadata
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/charges/initialize", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("status", out var status) && status.GetBoolean())
            {
                var data = root.GetProperty("data");
                var checkoutUrl = GetString(data, "checkout_url")
                    ?? GetString(data, "redirect_url")
                    ?? string.Empty;

                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = GetString(root, "message") ?? "Payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = checkoutUrl,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "reference", GetString(data, "reference") ?? txRef }
                    }
                };
            }

            return new PaymentResponse
            {
                Success = false,
                Message = GetString(root, "message") ?? "Korapay payment initiation failed",
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Korapay payment");
            throw new Exception("Failed to create payment with Korapay", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Korapay payment: {Reference}", SanitizeForLog(transactionReference));

        try
        {
            var response = await _httpClient.GetAsync($"/charges/{transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("status", out var status) && status.GetBoolean())
            {
                var data = root.GetProperty("data");
                var statusText = GetString(data, "status")?.ToLowerInvariant() ?? string.Empty;
                var paymentStatus = statusText switch
                {
                    "success" or "successful" => PaymentStatus.Successful,
                    "failed" => PaymentStatus.Failed,
                    "cancelled" or "canceled" or "expired" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = GetString(root, "message") ?? "Payment verification successful",
                    Status = paymentStatus,
                    Amount = GetDecimal(data, "amount"),
                    Currency = GetString(data, "currency") ?? string.Empty,
                    PaymentDate = GetDateTime(data, "paid_at") ?? GetDateTime(data, "created_at") ?? DateTime.UtcNow
                };
            }

            return new VerificationResponse
            {
                Success = false,
                TransactionReference = transactionReference,
                Message = GetString(root, "message") ?? "Korapay verification failed",
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Korapay payment");
            throw new Exception("Failed to verify payment with Korapay", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Korapay refund for transaction: {Reference}", SanitizeForLog(request.TransactionReference));

        var payload = new
        {
            reference = request.TransactionReference,
            amount = request.Amount,
            reason = request.Reason
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/refunds", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("status", out var status) && status.GetBoolean())
            {
                var data = root.GetProperty("data");
                return new RefundResponse
                {
                    Success = true,
                    TransactionReference = request.TransactionReference,
                    RefundReference = GetString(data, "reference") ?? string.Empty,
                    Message = GetString(root, "message") ?? "Refund processed successfully",
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    RefundDate = DateTime.UtcNow
                };
            }

            return new RefundResponse
            {
                Success = false,
                TransactionReference = request.TransactionReference,
                Message = GetString(root, "message") ?? "Korapay refund failed",
                Amount = request.Amount,
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Korapay refund");
            throw new Exception("Failed to process refund with Korapay", ex);
        }
    }

    private static string? GetString(JsonElement source, string propertyName)
    {
        return source.TryGetProperty(propertyName, out var property) ? property.GetString() : null;
    }

    private static decimal GetDecimal(JsonElement source, string propertyName)
    {
        return source.TryGetProperty(propertyName, out var property) && property.TryGetDecimal(out var value) ? value : 0;
    }

    private static DateTime? GetDateTime(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        var value = property.GetString();
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
