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
/// Squad payment gateway by GTCo — Nigeria.
/// Supports card, USSD, bank transfer, and virtual accounts in NGN.
/// Docs: https://squadinc.gitbook.io/squad-api-documentation
/// </summary>
public class SquadGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SquadGateway> _logger;

    // Squad has separate base URLs for live and sandbox
    private const string LiveUrl = "https://api-d.squadco.com";
    private const string SandboxUrl = "https://sandbox-api-d.squadco.com";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Squad;

    public SquadGateway(IOptions<PaymentGatewayConfig> config, ILogger<SquadGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Squad.SecretKey))
            throw new InvalidOperationException("Squad SecretKey is required");

        var baseUrl = _config.Squad.IsSandbox ? SandboxUrl : LiveUrl;

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.Squad.SecretKey);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Squad payment for customer {Email}", request.CustomerEmail);

        try
        {
            var txRef = $"SQ_{Guid.NewGuid():N}";

            var squadRequest = new
            {
                email = request.CustomerEmail,
                amount = (int)(request.Amount * 100), // Squad expects amount in kobo
                initiate_type = "inline",
                currency = "NGN",
                transaction_ref = txRef,
                customer_name = request.CustomerName,
                phone_number = request.CustomerPhone,
                redirect_link = request.RedirectUrl,
                payment_channels = new[] { "card", "bank", "ussd", "transfer" },
                pass_charge = false,
                metadata = new
                {
                    app_name = request.AppName,
                    extra = request.Metadata
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(squadRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transaction/initiate", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Squad create payment response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode &&
                root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("checkout_url", out var checkoutUrl))
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "Squad payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = checkoutUrl.GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "transaction_ref", txRef }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("Squad payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Squad payment");
            throw new Exception("Failed to create payment with Squad", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Squad payment: {Reference}", transactionReference);

        try
        {
            var response = await _httpClient.GetAsync($"/transaction/verify/{transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Squad verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("data", out var data))
            {
                var statusStr = data.TryGetProperty("transaction_status", out var statusProp)
                    ? statusProp.GetString()?.ToUpper() ?? string.Empty
                    : string.Empty;

                var paymentStatus = statusStr switch
                {
                    "SUCCESS" => PaymentStatus.Successful,
                    "FAILED" => PaymentStatus.Failed,
                    "ABANDONED" => PaymentStatus.Cancelled,
                    "EXPIRED" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    // Squad returns amount in kobo — convert back to naira
                    Amount = data.TryGetProperty("transaction_amount", out var amount)
                        ? amount.GetDecimal() / 100
                        : 0,
                    Currency = "NGN",
                    PaymentDate = data.TryGetProperty("created_at", out var createdAt)
                        && !string.IsNullOrEmpty(createdAt.GetString())
                        ? DateTime.Parse(createdAt.GetString()!)
                        : DateTime.UtcNow,
                    PaymentMethod = data.TryGetProperty("payment_type", out var method)
                        ? method.GetString() ?? string.Empty
                        : string.Empty,
                    Fee = data.TryGetProperty("transaction_charge", out var fee)
                        ? fee.GetDecimal() / 100
                        : 0
                };
            }

            var errorMessage = root.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Verification failed"
                : "Verification request failed";

            return new VerificationResponse
            {
                Success = false,
                TransactionReference = transactionReference,
                Message = errorMessage,
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Squad payment");
            throw new Exception("Failed to verify payment with Squad", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Squad refund for: {Reference}", request.TransactionReference);

        try
        {
            var refundRequest = new
            {
                transaction_ref = request.TransactionReference,
                refund_type = 1, // 1 = partial or full refund
                reason_for_refund = request.Reason ?? "Customer request",
                refund_amount = (int)(request.Amount * 100) // in kobo
            };

            var content = new StringContent(
                JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/transaction/refund", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Squad refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("data", out var data))
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = data.TryGetProperty("refund_id", out var refId)
                        ? refId.GetString() ?? string.Empty
                        : string.Empty,
                    TransactionReference = request.TransactionReference,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund processed successfully",
                    RefundDate = DateTime.UtcNow
                };
            }

            var errorMessage = root.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Refund failed"
                : "Refund request failed";

            return new RefundResponse { Success = false, Message = errorMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Squad refund");
            throw new Exception("Failed to process refund with Squad", ex);
        }
    }
}
