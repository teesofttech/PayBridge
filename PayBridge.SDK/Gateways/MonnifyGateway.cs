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
/// Monnify payment gateway (by Moniepoint) — Nigeria.
/// Supports bank transfer and card payments in NGN.
/// Docs: https://developers.monnify.com/
/// </summary>
public class MonnifyGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MonnifyGateway> _logger;
    private const string BaseUrl = "https://api.monnify.com";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Monnify;

    public MonnifyGateway(IOptions<PaymentGatewayConfig> config, ILogger<MonnifyGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Monnify.ApiKey))
            throw new InvalidOperationException("Monnify ApiKey is required");

        if (string.IsNullOrEmpty(_config.Monnify.SecretKey))
            throw new InvalidOperationException("Monnify SecretKey is required");

        if (string.IsNullOrEmpty(_config.Monnify.ContractCode))
            throw new InvalidOperationException("Monnify ContractCode is required");

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // -----------------------------------------------------------------------
    // Monnify uses Basic Auth with Base64(ApiKey:SecretKey) for most calls.
    // Access token is obtained once and reused per request cycle.
    // -----------------------------------------------------------------------

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config.Monnify.ApiKey}:{_config.Monnify.SecretKey}"));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        tokenRequest.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(tokenRequest);
        var body = await response.Content.ReadAsStringAsync();

        // Avoid logging the raw body; it may contain an access token.
        _logger.LogDebug("Monnify auth response status: {StatusCode}", (int)response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (response.IsSuccessStatusCode &&
            root.TryGetProperty("responseBody", out var responseBody) &&
            responseBody.TryGetProperty("accessToken", out var token))
        {
            return token.GetString() ?? throw new InvalidOperationException("Monnify returned an empty access token");
        }

        var errorMessage = root.TryGetProperty("responseMessage", out var msg)
            ? msg.GetString()
            : null;

        throw new InvalidOperationException($"Failed to obtain Monnify access token. StatusCode={(int)response.StatusCode}. {errorMessage}");
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Monnify payment for customer {Email}", request.CustomerEmail);

        try
        {
            var accessToken = await GetAccessTokenAsync();
            var txRef = $"MNF_{Guid.NewGuid():N}";
            var currency = request.Currency?.ToUpperInvariant();
            if (currency != "NGN")
                throw new InvalidOperationException($"Monnify only supports NGN payments (received '{request.Currency}').");

            var monnifyRequest = new
            {
                amount = request.Amount,
                customerName = request.CustomerName,
                customerEmail = request.CustomerEmail,
                paymentReference = txRef,
                paymentDescription = request.AppName ?? "Payment",
                currencyCode = currency,
                contractCode = _config.Monnify.ContractCode,
                redirectUrl = request.RedirectUrl,
                paymentMethods = new[] { "CARD", "ACCOUNT_TRANSFER" }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchant/transactions/init-transaction");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(monnifyRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Monnify create payment response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode &&
                root.TryGetProperty("responseBody", out var data) &&
                data.TryGetProperty("checkoutUrl", out var checkoutUrl))
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "Monnify payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = checkoutUrl.GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "transaction_reference", data.TryGetProperty("transactionReference", out var tRef) ? tRef.GetString() ?? string.Empty : string.Empty }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("responseMessage", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("Monnify payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Monnify payment");
            throw new Exception("Failed to create payment with Monnify", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Monnify payment: {Reference}", transactionReference);

        try
        {
            var accessToken = await GetAccessTokenAsync();

            // Monnify requires URL-encoding the reference
            var encodedRef = Uri.EscapeDataString(transactionReference);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v2/merchant/transactions/query?paymentReference={encodedRef}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Monnify verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("responseBody", out var data))
            {
                var statusStr = data.TryGetProperty("paymentStatus", out var statusProp)
                    ? statusProp.GetString()?.ToUpper() ?? string.Empty
                    : string.Empty;

                var paymentStatus = statusStr switch
                {
                    "PAID" => PaymentStatus.Successful,
                    "FAILED" => PaymentStatus.Failed,
                    "CANCELLED" => PaymentStatus.Cancelled,
                    "OVERPAID" => PaymentStatus.Successful,
                    "PARTIALLY_PAID" => PaymentStatus.Pending,
                    _ => PaymentStatus.Pending
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = "Payment verification successful",
                    Status = paymentStatus,
                    Amount = data.TryGetProperty("amountPaid", out var amount) ? amount.GetDecimal() : 0,
                    Currency = "NGN",
                    PaymentDate = data.TryGetProperty("completedOn", out var completedOn) && !string.IsNullOrEmpty(completedOn.GetString())
                        ? DateTime.Parse(completedOn.GetString()!)
                        : DateTime.UtcNow,
                    PaymentMethod = data.TryGetProperty("paymentMethod", out var method) ? method.GetString() ?? string.Empty : string.Empty,
                    Fee = data.TryGetProperty("fee", out var fee) ? fee.GetDecimal() : 0
                };
            }

            var errorMessage = root.TryGetProperty("responseMessage", out var msg)
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
            _logger.LogError(ex, "Error verifying Monnify payment");
            throw new Exception("Failed to verify payment with Monnify", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Monnify refund for: {Reference}", request.TransactionReference);

        try
        {
            var accessToken = await GetAccessTokenAsync();
            var refundRef = $"REF_{Guid.NewGuid():N}";

            var refundRequest = new
            {
                transactionReference = request.TransactionReference,
                refundReference = refundRef,
                refundAmount = request.Amount,
                refundReason = request.Reason ?? "Customer request",
                customerNote = request.Reason ?? "Refund initiated",
                destinationAccountNumber = string.Empty, // Optional: for bank transfer refunds
                destinationAccountBankCode = string.Empty
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchant/refunds/initiate-refund");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Monnify refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("responseBody", out var data))
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = data.TryGetProperty("refundReference", out var refId) ? refId.GetString() ?? refundRef : refundRef,
                    TransactionReference = request.TransactionReference,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund initiated successfully",
                    RefundDate = DateTime.UtcNow
                };
            }

            var errorMessage = root.TryGetProperty("responseMessage", out var msg)
                ? msg.GetString() ?? "Refund failed"
                : "Refund request failed";

            return new RefundResponse { Success = false, Message = errorMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Monnify refund");
            throw new Exception("Failed to process refund with Monnify", ex);
        }
    }
}
