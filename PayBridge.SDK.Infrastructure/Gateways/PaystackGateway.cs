using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Domain.Enums;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PayBridge.SDK.Infrastructure.Gateways;
public class PaystackGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaystackGateway> _logger;
    private readonly string _baseUrl = "https://api.paystack.co";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Paystack;

    public PaystackGateway(IOptions<PaymentGatewayConfig> config, ILogger<PaystackGateway> logger)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Paystack.SecretKey))
        {
            throw new InvalidOperationException("Paystack secret key is required");
        }

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Paystack.SecretKey);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Paystack payment for customer {Email}", request.CustomerEmail);

        try
        {
            // Prepare request payload for Paystack
            var paystackRequest = new
            {
                amount = (int)(request.Amount * 100), // Paystack expects amount in kobo (smallest currency unit)
                email = request.CustomerEmail,
                currency = request.Currency,
                callback_url = request.RedirectUrl,
                metadata = new
                {
                    custom_fields = new[]
                    {
                        new {

                            display_name = "Customer Name", variable_name = "customer_name", value = request.CustomerName
                        },
                        new {
                            display_name = "Customer Phone", variable_name = "customer_phone", value = request.CustomerPhone
                        }
                    },
                    additional_info = request.Metadata
                },
                reference = $"PS_{Guid.NewGuid().ToString("N")}"
            };

            // Serialize and send the request
            var content = new StringContent(
                JsonSerializer.Serialize(paystackRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/transaction/initialize", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Paystack response: {Response}", responseBody);

            // Parse the response
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.GetProperty("status").GetBoolean())
            {
                var data = root.GetProperty("data");
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = paystackRequest.reference,
                    Message = "Payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = data.GetProperty("authorization_url").GetString(),
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "access_code", data.GetProperty("access_code").GetString() }
                    }
                };
            }
            else
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : "Unknown error occurred";

                _logger.LogError("Paystack payment initiation failed: {Error}", errorMessage);

                return new PaymentResponse
                {
                    Success = false,
                    Message = errorMessage,
                    Status = PaymentStatus.Failed
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Paystack payment");
            throw new Exception("Failed to create payment with Paystack", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Paystack payment: {Reference}", transactionReference);

        try
        {
            var response = await _httpClient.GetAsync($"/transaction/verify/{transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Paystack verification response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.GetProperty("status").GetBoolean())
            {
                var data = root.GetProperty("data");

                var statusStr = data.GetProperty("status").GetString()!.ToLower()!;
                var paymentStatus = statusStr switch
                {
                    "success" => PaymentStatus.Successful,
                    "failed" => PaymentStatus.Failed,
                    "abandoned" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = data.GetProperty("amount").GetDecimal() / 100,
                    AmountSettled = data.GetProperty("amount").GetDecimal() / 100,
                    Currency = data.GetProperty("currency").GetString(),
                    PaymentDate = data.TryGetProperty("paid_at", out var paidAt) && !string.IsNullOrEmpty(paidAt.GetString())
                        ? DateTime.Parse(paidAt.GetString())
                        : DateTime.UtcNow,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "channel", data.GetProperty("channel").GetString() },
                        { "gateway_response", data.GetProperty("gateway_response").GetString() },
                    },
                    PaymentMethod = data.GetProperty("channel").GetString(),
                    Fee = data.TryGetProperty("fees", out var fee) ? fee.GetDecimal() / 100 : 0,
                    Metadata = data.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("additional_info", out var additionalInfo)
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(additionalInfo.GetRawText())!
                        : new Dictionary<string, string>()

                };
            }
            else
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : "Unknown error occurred during verification";

                _logger.LogError("Paystack verification failed: {Error}", errorMessage);

                return new VerificationResponse
                {
                    Success = false,
                    TransactionReference = transactionReference,
                    Message = errorMessage,
                    Status = PaymentStatus.Failed
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Paystack payment");
            throw new Exception("Failed to verify payment with Paystack", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing refund for transaction: {Reference}", request.TransactionReference);

        try
        {
            // Prepare refund request for Paystack
            var paystackRequest = new
            {
                transaction = request.TransactionReference,
                amount = request.Amount > 0 ? (int)(request.Amount * 100) : 0, // Optional amount in kobo
                reason = request.Reason
            };

            var content = new StringContent(
                JsonSerializer.Serialize(paystackRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/refund", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Paystack refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.GetProperty("status").GetBoolean())
            {
                var data = root.GetProperty("data");

                return new RefundResponse
                {
                    Success = true,
                    RefundReference = data.GetProperty("id").GetString(),
                    Message = "Refund processed successfully",
                    Amount = data.GetProperty("amount").GetDecimal() / 100, // Convert from kobo
                    Status = PaymentStatus.Refunded,
                    RefundDate = data.TryGetProperty("created_at", out var createdAt)
                        ? DateTime.Parse(createdAt.GetString())
                        : DateTime.UtcNow
                };
            }
            else
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : "Unknown error occurred during refund";

                _logger.LogError("Paystack refund failed: {Error}", errorMessage);

                return new RefundResponse
                {
                    Success = false,
                    Message = errorMessage,
                    Status = PaymentStatus.Failed
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Paystack refund");
            throw new Exception("Failed to process refund with Paystack", ex);
        }
    }

}
