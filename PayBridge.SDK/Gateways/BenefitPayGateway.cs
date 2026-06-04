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
/// BenefitPay gateway — Bahrain's national payment network (BHD currency).
/// Docs: https://www.benefitpay.bh/developers
/// </summary>
public class BenefitPayGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BenefitPayGateway> _logger;
    private const string BaseUrl = "https://pgw.benefitpay.bh";

    public PaymentGatewayType GatewayType => PaymentGatewayType.BenefitPay;

    public BenefitPayGateway(IOptions<PaymentGatewayConfig> config, ILogger<BenefitPayGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.BenefitPay.MerchantId))
            throw new InvalidOperationException("BenefitPay MerchantId is required");

        if (string.IsNullOrEmpty(_config.BenefitPay.ApiKey))
            throw new InvalidOperationException("BenefitPay ApiKey is required");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _config.BenefitPay.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Merchant-Id", _config.BenefitPay.MerchantId);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating BenefitPay payment for customer {Email}", request.CustomerEmail);

        try
        {
            var txRef = $"BP_{Guid.NewGuid():N}";

            var benefitRequest = new
            {
                merchant_id = _config.BenefitPay.MerchantId,
                order_id = txRef,
                amount = request.Amount,
                currency = "BHD",
                description = request.AppName ?? "Payment",
                redirect_url = request.RedirectUrl,
                customer = new
                {
                    email = request.CustomerEmail,
                    name = request.CustomerName,
                    phone = request.CustomerPhone
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(benefitRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/payment/initiate", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("BenefitPay response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("payment_url", out var paymentUrl))
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "BenefitPay payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = paymentUrl.GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "order_id", txRef }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("BenefitPay payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating BenefitPay payment");
            throw new Exception("Failed to create payment with BenefitPay", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying BenefitPay payment: {Reference}", transactionReference);

        try
        {
            var response = await _httpClient.GetAsync($"/payment/status?order_id={transactionReference}");
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("BenefitPay verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                var statusStr = root.TryGetProperty("status", out var status)
                    ? status.GetString()?.ToLower() ?? string.Empty
                    : string.Empty;

                var paymentStatus = statusStr switch
                {
                    "success" or "paid" => PaymentStatus.Successful,
                    "failed" or "declined" => PaymentStatus.Failed,
                    "cancelled" => PaymentStatus.Cancelled,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = root.TryGetProperty("amount", out var amount) ? amount.GetDecimal() : 0,
                    Currency = "BHD",
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
            _logger.LogError(ex, "Error verifying BenefitPay payment");
            throw new Exception("Failed to verify payment with BenefitPay", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing BenefitPay refund for: {Reference}", request.TransactionReference);

        try
        {
            var refundRequest = new
            {
                order_id = request.TransactionReference,
                amount = request.Amount,
                reason = request.Reason ?? "Customer request"
            };

            var content = new StringContent(JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/payment/refund", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("BenefitPay refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = root.TryGetProperty("refund_id", out var refId) ? refId.GetString() ?? string.Empty : string.Empty,
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
            _logger.LogError(ex, "Error processing BenefitPay refund");
            throw new Exception("Failed to process refund with BenefitPay", ex);
        }
    }
}
