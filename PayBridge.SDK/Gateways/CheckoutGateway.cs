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

public class CheckoutGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CheckoutGateway> _logger;
    private const string BaseUrl = "https://api.checkout.com";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Checkout;

    public CheckoutGateway(IOptions<PaymentGatewayConfig> config, ILogger<CheckoutGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Checkout.SecretKey))
            throw new InvalidOperationException("Checkout.com secret key is required");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.Checkout.SecretKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Checkout.com payment for customer {Email}", request.CustomerEmail);

        try
        {
            var txRef = $"CO_{Guid.NewGuid():N}";

            var checkoutRequest = new
            {
                amount = (int)(request.Amount * 100),
                currency = request.Currency.ToUpper(),
                reference = txRef,
                description = request.AppName ?? "Payment",
                customer = new
                {
                    email = request.CustomerEmail,
                    name = request.CustomerName
                },
                success_url = request.RedirectUrl,
                failure_url = request.RedirectUrl,
                metadata = new { customer_phone = request.CustomerPhone }
            };

            var content = new StringContent(JsonSerializer.Serialize(checkoutRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/payment-links", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Checkout.com response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "Payment link created successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = root.GetProperty("_links").GetProperty("redirect").GetProperty("href").GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "payment_link_id", root.GetProperty("id").GetString() ?? string.Empty }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("error_type", out var errorType)
                ? errorType.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("Checkout.com payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Checkout.com payment");
            throw new Exception("Failed to create payment with Checkout.com", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Checkout.com payment: {Reference}", transactionReference);

        try
        {
            var response = await _httpClient.GetAsync($"/payments?reference={transactionReference}&limit=1");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Checkout.com verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
            {
                var payment = dataArray[0];
                var statusStr = payment.GetProperty("status").GetString()?.ToLower() ?? string.Empty;

                var paymentStatus = statusStr switch
                {
                    "captured" => PaymentStatus.Successful,
                    "declined" => PaymentStatus.Failed,
                    "canceled" => PaymentStatus.Cancelled,
                    "voided" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = payment.GetProperty("amount").GetDecimal() / 100,
                    Currency = payment.GetProperty("currency").GetString()?.ToUpper() ?? string.Empty,
                    PaymentDate = payment.TryGetProperty("processed_on", out var processedOn)
                        ? DateTime.Parse(processedOn.GetString()!)
                        : DateTime.UtcNow
                };
            }

            return new VerificationResponse
            {
                Success = false,
                TransactionReference = transactionReference,
                Message = "Transaction not found",
                Status = PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Checkout.com payment");
            throw new Exception("Failed to verify payment with Checkout.com", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Checkout.com refund for: {Reference}", request.TransactionReference);

        try
        {
            // Find the payment id first
            var lookupResponse = await _httpClient.GetAsync($"/payments?reference={request.TransactionReference}&limit=1");
            var lookupBody = await lookupResponse.Content.ReadAsStringAsync();

            using var lookupDoc = JsonDocument.Parse(lookupBody);
            var payments = lookupDoc.RootElement.GetProperty("data");

            if (payments.GetArrayLength() == 0)
                return new RefundResponse { Success = false, Message = "Transaction not found in Checkout.com" };

            var paymentId = payments[0].GetProperty("id").GetString();

            var refundRequest = new { amount = (int)(request.Amount * 100), reference = $"REF_{Guid.NewGuid():N}" };
            var content = new StringContent(JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/payments/{paymentId}/refunds", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Checkout.com refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = root.TryGetProperty("reference", out var refId) ? refId.GetString() ?? string.Empty : string.Empty,
                    TransactionReference = request.TransactionReference,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund processed successfully",
                    RefundDate = DateTime.UtcNow
                };
            }

            return new RefundResponse { Success = false, Message = "Refund request failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Checkout.com refund");
            throw new Exception("Failed to process refund with Checkout.com", ex);
        }
    }
}
