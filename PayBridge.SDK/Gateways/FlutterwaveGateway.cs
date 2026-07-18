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
    private readonly ILogger<FlutterwaveGateway> _logger;
    private readonly string _baseUrl = "https://api.flutterwave.com";
    public FlutterwaveGateway(IOptions<PaymentGatewayConfig> config, ILogger<FlutterwaveGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        if (string.IsNullOrEmpty(_config.FlutterwaveConfig.SecretKey))
        {
            throw new InvalidOperationException("Flutterwave secret key is required");
        }
        _logger = logger;

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.FlutterwaveConfig.SecretKey);
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.Flutterwave;
    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Flutterwave payment for customer {Email}", request.CustomerEmail);

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

            if (response.IsSuccessStatusCode && root.GetProperty("status").GetString()?.ToLower() == "success")
            {
                var data = root.GetProperty("data");
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = flutterwaveRequest.tx_ref,
                    Message = root.GetProperty("message").GetString() ?? string.Empty,
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = data.GetProperty("link").GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "link", data.GetProperty("link").GetString() ?? string.Empty }
                    }
                };
            }
            else
            {
                string errorMessage = root.TryGetProperty("message", out var message)
                    ? message.GetString() ?? "Unknown error occurred"
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

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TransactionReference))
        {
            return FailedRefund(request, "Flutterwave transaction reference is required");
        }

        if (request.Amount <= 0)
        {
            return FailedRefund(request, "Flutterwave refund amount must be greater than zero");
        }

        try
        {
            var encodedReference = Uri.EscapeDataString(request.TransactionReference);
            using var verificationResponse = await _httpClient.GetAsync(
                $"/v3/transactions/verify_by_reference?tx_ref={encodedReference}");
            var verificationBody = await verificationResponse.Content.ReadAsStringAsync();

            using var verificationDocument = JsonDocument.Parse(verificationBody);
            var verificationRoot = verificationDocument.RootElement;
            if (!verificationResponse.IsSuccessStatusCode ||
                !IsSuccessfulRoot(verificationRoot) ||
                !verificationRoot.TryGetProperty("data", out var transaction) ||
                !TryGetTransactionId(transaction, out var transactionId))
            {
                return FailedRefund(
                    request,
                    GetMessage(verificationRoot, "Flutterwave transaction could not be resolved"));
            }

            var transactionStatus = GetString(transaction, "status");
            if (!string.Equals(transactionStatus, "successful", StringComparison.OrdinalIgnoreCase))
            {
                return FailedRefund(request, "Only successful Flutterwave transactions can be refunded");
            }

            if (transaction.TryGetProperty("amount", out var transactionAmountElement) &&
                transactionAmountElement.TryGetDecimal(out var transactionAmount) &&
                request.Amount > transactionAmount)
            {
                return FailedRefund(request, "Refund amount exceeds the Flutterwave transaction amount");
            }

            var refundPayload = new
            {
                amount = request.Amount,
                comments = string.IsNullOrWhiteSpace(request.Reason)
                    ? "requested_by_customer"
                    : request.Reason
            };
            using var content = new StringContent(
                JsonSerializer.Serialize(refundPayload),
                Encoding.UTF8,
                "application/json");
            using var refundResponse = await _httpClient.PostAsync(
                $"/v3/transactions/{transactionId}/refund",
                content);
            var refundBody = await refundResponse.Content.ReadAsStringAsync();

            using var refundDocument = JsonDocument.Parse(refundBody);
            var root = refundDocument.RootElement;
            if (!refundResponse.IsSuccessStatusCode ||
                !IsSuccessfulRoot(root) ||
                !root.TryGetProperty("data", out var data))
            {
                return FailedRefund(request, GetMessage(root, "Flutterwave refund was rejected"));
            }

            var providerStatus = GetString(data, "status").ToLowerInvariant();
            var failed = providerStatus is "failed" or "error";
            var pending = providerStatus is "new" or "pending" or "processing";
            var responseStatus = failed
                ? PaymentStatus.Failed
                : pending
                    ? PaymentStatus.Pending
                    : PaymentStatus.Refunded;
            var refundReference = GetString(data, "flw_ref");
            if (string.IsNullOrWhiteSpace(refundReference) &&
                data.TryGetProperty("id", out var refundId))
            {
                refundReference = refundId.ToString();
            }

            var refundType = transaction.TryGetProperty("amount", out var originalAmountElement) &&
                originalAmountElement.TryGetDecimal(out var originalAmount) &&
                request.Amount == originalAmount
                    ? "full"
                    : "partial";

            return new RefundResponse
            {
                Success = !failed,
                RefundReference = refundReference,
                TransactionReference = request.TransactionReference,
                Message = failed
                    ? GetMessage(root, "Flutterwave refund failed")
                    : $"Flutterwave {refundType} refund {providerStatus}",
                Amount = data.TryGetProperty("amount_refunded", out var refundedAmount) &&
                    refundedAmount.TryGetDecimal(out var parsedAmount)
                        ? parsedAmount
                        : request.Amount,
                Status = responseStatus,
                RefundDate = TryGetDate(data, "created_at") ?? DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Flutterwave refund request timed out for {Reference}",
                request.TransactionReference);
            return FailedRefund(request, "Flutterwave refund request timed out");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Flutterwave returned an invalid refund response for {Reference}",
                request.TransactionReference);
            return FailedRefund(request, "Flutterwave returned an invalid response");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Flutterwave refund request failed for {Reference}",
                request.TransactionReference);
            return FailedRefund(request, "Flutterwave refund request failed");
        }
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
        if (response.IsSuccessStatusCode && root.GetProperty("status").GetString()?.ToLower() == "success")
        {
            var data = root.GetProperty("data");

            // Determine payment status based on Flutterwave response
            var statusStr = data.GetProperty("status").GetString()?.ToLower() ?? string.Empty;
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
                        ? DateTime.Parse(paidAt.GetString()!)
                        : DateTime.UtcNow,
                Currency = data.GetProperty("currency").GetString() ?? string.Empty,
                Fee = data.TryGetProperty("app_fee", out var fee) ? fee.GetDecimal() : 0,
                PaymentMethod = data.TryGetProperty("payment_type", out var paymentMethod) ? paymentMethod.GetString() ?? string.Empty : string.Empty,
                AmountSettled = data.TryGetProperty("amount_settled", out var amountSettled) ? amountSettled.GetDecimal() : 0,
                Metadata = data.TryGetProperty("meta", out var meta) ? JsonSerializer.Deserialize<Dictionary<string, string>>(meta.GetRawText()) ?? new Dictionary<string, string>() : new Dictionary<string, string>()
            };
        }
        else
        {
            string errorMessage = root.TryGetProperty("message", out var message)
                ? message.GetString() ?? "Unknown error occurred during verification"
                : "Unknown error occurred during verification";

            _logger.LogError("Flutterwave verification failed: {Error}", errorMessage);

            return new VerificationResponse
            {
                Success = false,
                TransactionReference = transactionReference,
                Message = errorMessage,
                Status = PaymentStatus.Failed
            };
        }
    }

    private static bool IsSuccessfulRoot(JsonElement root) =>
        string.Equals(GetString(root, "status"), "success", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetTransactionId(JsonElement data, out string transactionId)
    {
        transactionId = data.TryGetProperty("id", out var id) &&
            id.ValueKind is JsonValueKind.Number or JsonValueKind.String
                ? id.ToString()
                : string.Empty;
        return !string.IsNullOrWhiteSpace(transactionId);
    }

    private static string GetMessage(JsonElement root, string fallback)
    {
        var message = GetString(root, "message");
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private static string GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static DateTime? TryGetDate(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTime.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static RefundResponse FailedRefund(RefundRequest request, string message) => new()
    {
        Success = false,
        TransactionReference = request.TransactionReference,
        Message = message,
        Amount = request.Amount,
        Status = PaymentStatus.Failed
    };
}
