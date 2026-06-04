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

public class StripeGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StripeGateway> _logger;
    private const string BaseUrl = "https://api.stripe.com";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Stripe;

    public StripeGateway(IOptions<PaymentGatewayConfig> config, ILogger<StripeGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Stripe.SecretKey))
            throw new InvalidOperationException("Stripe secret key is required");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.Stripe.SecretKey);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Stripe payment session for customer {Email}", request.CustomerEmail);

        try
        {
            var txRef = $"ST_{Guid.NewGuid():N}";

            // Stripe uses form-encoded body for its API
            var formData = new Dictionary<string, string>
            {
                ["payment_method_types[]"] = "card",
                ["mode"] = "payment",
                ["line_items[0][price_data][currency]"] = request.Currency.ToLower(),
                ["line_items[0][price_data][unit_amount]"] = ((int)(request.Amount * 100)).ToString(),
                ["line_items[0][price_data][product_data][name]"] = request.AppName ?? "Payment",
                ["line_items[0][quantity]"] = "1",
                ["success_url"] = $"{request.RedirectUrl}?session_id={{CHECKOUT_SESSION_ID}}&ref={txRef}",
                ["cancel_url"] = request.RedirectUrl ?? string.Empty,
                ["customer_email"] = request.CustomerEmail,
                ["client_reference_id"] = txRef
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync("/v1/checkout/sessions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Stripe response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "Payment session created successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = root.GetProperty("url").GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "session_id", root.GetProperty("id").GetString() ?? string.Empty }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("error", out var error)
                ? error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("Stripe payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe payment");
            throw new Exception("Failed to create payment with Stripe", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Stripe payment: {Reference}", transactionReference);

        try
        {
            // Retrieve the checkout session by client_reference_id via search
            var response = await _httpClient.GetAsync(
                $"/v1/checkout/sessions?client_reference_id={transactionReference}&limit=1");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Stripe verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                var data = root.GetProperty("data");
                if (data.GetArrayLength() == 0)
                {
                    return new VerificationResponse
                    {
                        Success = false,
                        TransactionReference = transactionReference,
                        Message = "Transaction not found",
                        Status = PaymentStatus.Failed
                    };
                }

                var session = data[0];
                var paymentStatus = session.GetProperty("payment_status").GetString() switch
                {
                    "paid" => PaymentStatus.Successful,
                    "unpaid" => PaymentStatus.Pending,
                    "no_payment_required" => PaymentStatus.Successful,
                    _ => PaymentStatus.Failed
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = session.GetProperty("amount_total").GetDecimal() / 100,
                    Currency = session.GetProperty("currency").GetString()?.ToUpper() ?? string.Empty,
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
            _logger.LogError(ex, "Error verifying Stripe payment");
            throw new Exception("Failed to verify payment with Stripe", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Stripe refund for transaction: {Reference}", request.TransactionReference);

        try
        {
            // First we need to find the PaymentIntent from the session
            var sessionResponse = await _httpClient.GetAsync(
                $"/v1/checkout/sessions?client_reference_id={request.TransactionReference}&limit=1");
            var sessionBody = await sessionResponse.Content.ReadAsStringAsync();

            using var sessionDoc = JsonDocument.Parse(sessionBody);
            var sessions = sessionDoc.RootElement.GetProperty("data");

            if (sessions.GetArrayLength() == 0)
            {
                return new RefundResponse { Success = false, Message = "Transaction not found in Stripe" };
            }

            var paymentIntent = sessions[0].GetProperty("payment_intent").GetString();

            var formData = new Dictionary<string, string>
            {
                ["payment_intent"] = paymentIntent ?? string.Empty,
                ["amount"] = ((int)(request.Amount * 100)).ToString()
            };

            var response = await _httpClient.PostAsync("/v1/refunds", new FormUrlEncodedContent(formData));
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Stripe refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = root.GetProperty("id").GetString() ?? string.Empty,
                    TransactionReference = request.TransactionReference,
                    Amount = root.GetProperty("amount").GetDecimal() / 100,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund processed successfully"
                };
            }

            var errorMessage = root.TryGetProperty("error", out var error)
                ? error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error"
                : "Unknown error occurred";

            return new RefundResponse { Success = false, Message = errorMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe refund");
            throw new Exception("Failed to process refund with Stripe", ex);
        }
    }
}
