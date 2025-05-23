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
public class FlutterwaveGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaystackGateway> _logger;
    private readonly string _baseUrl = "https://api.flutterwave.com";
    public FlutterwaveGateway(IOptions<PaymentGatewayConfig> config, ILogger<PaystackGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        if (string.IsNullOrEmpty(_config.FlutterwaveConfig.SecretKey))
        {
            throw new InvalidOperationException("Paystack secret key is required");
        }
        _logger = logger;

        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.FlutterwaveConfig.SecretKey);
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.Flutterwave;
    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Paystack payment for customer {Email}", request.CustomerEmail);

        try
        {
            // Prepare request payload for Paystack
            var flutterwaveRequest = new
            {
                amount = request.Amount,
                tx_ref = $"FLW_{Guid.NewGuid():N}", // Unique transaction reference
                currency = request.Currency,
                redirect_url = request.RedirectUrl,
                customer = new
                {
                    email = request.CustomerEmail,
                    phone_number = request.CustomerPhone,
                    name = request.CustomerName
                },
                customizations = new
                {
                    title = request.AppName,
                    logo = request.Logo
                },
                configuration = new
                {
                    session_duration = 30
                },
                max_retry_attempt = 5,
                payment_options = "card, ussd",
                link_expiration = DateTime.UtcNow.AddMinutes(10),
                meta = request.Metadata
            };

            // Serialize and send the request
            var content = new StringContent(
                JsonSerializer.Serialize(flutterwaveRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/v3/payments", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Flutterwave response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.GetProperty("status").GetString().ToLower() == "success")
            {
                var data = root.GetProperty("data");
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = flutterwaveRequest.tx_ref, // Make sure you pass this in or store it earlier
                    Message = root.GetProperty("message").GetString(),
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = data.GetProperty("link").GetString(),
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "link", data.GetProperty("link").GetString() }
                    }
                };
            }
            else
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : "Unknown error occurred";

                _logger.LogError("Flutterwave payment initiation failed: {Error}", errorMessage);

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
            _logger.LogError(ex, "Error creating flutterwave payment");
            throw new Exception("Failed to create payment with flutterwave", ex);
        }

    }

    public Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<PaymentMethodResponse> SavePaymentMethodAsync(PaymentMethodRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        var response = await _httpClient.GetAsync($"/v3/transactions/verify_by_reference?tx_ref={transactionReference}");
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("Paystack response: {Response}", responseBody);

        using var jsonDocument = JsonDocument.Parse(responseBody);
        var root = jsonDocument.RootElement;
        if (response.IsSuccessStatusCode && root.GetProperty("status").GetString().ToLower() == "success")
        {
            var data = root.GetProperty("data");

            // Determine payment status based on Flutterwave response
            var statusStr = data.GetProperty("status").GetString().ToLower();
            var paymentStatus = statusStr switch
            {
                "successful" => PaymentStatus.Successful,
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
                Amount = data.GetProperty("amount").GetDecimal(),
                PaymentDate = data.TryGetProperty("created_at", out var paidAt) && !string.IsNullOrEmpty(paidAt.GetString())
                        ? DateTime.Parse(paidAt.GetString())
                        : DateTime.UtcNow,
                Currency = data.GetProperty("currency").GetString(),
                Fee = data.TryGetProperty("app_fee", out var fee) ? fee.GetDecimal() : 0,
                PaymentMethod = data.TryGetProperty("payment_type", out var paymentMethod) ? paymentMethod.GetString() : string.Empty,
                AmountSettled = data.TryGetProperty("amount_settled", out var amountSettled) ? amountSettled.GetDecimal() : 0,
                Metadata = data.TryGetProperty("meta", out var meta) ? JsonSerializer.Deserialize<Dictionary<string, string>>(meta.GetRawText()) : new Dictionary<string, string>()
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
}
