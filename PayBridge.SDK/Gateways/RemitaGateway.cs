using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Exceptions;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Gateways;

/// <summary>
/// Remita payment gateway implementation for Nigerian payments.
/// Supports government, corporate, and retail payments via Remita's API.
/// </summary>
public class RemitaGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemitaGateway> _logger;

    private const string LiveBaseUrl = "https://login.remita.net/remita";
    private const string SandboxBaseUrl = "https://remitademo.net/remita";
    private const string TxRefPrefix = "REM_";

    public RemitaGateway(
        PaymentGatewayConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<RemitaGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClientFactory.CreateClient(nameof(RemitaGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.Remita;

    private string BaseUrl => _config.Remita.IsSandbox ? SandboxBaseUrl : LiveBaseUrl;

    /// <summary>
    /// Generates Remita API hash: SHA512(merchantId + serviceTypeId + orderId + amount + apiKey)
    /// </summary>
    private string GenerateHash(string orderId, decimal amount)
    {
        var amountKobo = (long)(amount * 100);
        var raw = $"{_config.Remita.MerchantId}{_config.Remita.ServiceTypeId}{orderId}{amountKobo}{_config.Remita.ApiKey}";
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLower();
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var txRef = $"{TxRefPrefix}{Guid.NewGuid():N}";
            var hash = GenerateHash(txRef, request.Amount);
            var authToken = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config.Remita.MerchantId}:{_config.Remita.ApiKey}:{hash}"));

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",
                $"remitaConsumerKey={_config.Remita.MerchantId},remitaConsumerToken={authToken}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var payload = new
            {
                serviceTypeId = _config.Remita.ServiceTypeId,
                amount = (long)(request.Amount * 100),
                orderId = txRef,
                payerName = request.CustomerName ?? string.Empty,
                payerEmail = request.CustomerEmail,
                payerPhone = request.CustomerPhone ?? string.Empty,
                description = request.Description ?? "Payment",
                currency = request.Currency ?? "NGN"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/exapp/api/v1/send/api/echannelsvc/merchant/api/paymentinit", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Remita initiate response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var statusCode = root.TryGetProperty("statuscode", out var sc) ? sc.GetString() : null;

            if (statusCode != "025")
            {
                var msg = root.TryGetProperty("status", out var s) ? s.GetString() : "Unknown error";
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"Remita payment initiation failed: {msg}",
                    Status = PaymentStatus.Failed
                };
            }

            var rrr = root.TryGetProperty("RRR", out var rrrProp) ? rrrProp.GetString() : string.Empty;
            var checkoutUrl = _config.Remita.IsSandbox
                ? $"https://remitademo.net/remita/ecomm/finalize.reg?merchantId={_config.Remita.MerchantId}&hash={hash}&RRR={rrr}"
                : $"https://login.remita.net/remita/ecomm/finalize.reg?merchantId={_config.Remita.MerchantId}&hash={hash}&RRR={rrr}";

            return new PaymentResponse
            {
                Success = true,
                TransactionReference = txRef,
                CheckoutUrl = checkoutUrl,
                Message = "Payment initiated successfully",
                Status = PaymentStatus.Pending,
                GatewayResponse = new Dictionary<string, string> { ["RRR"] = rrr ?? string.Empty }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Remita payment");
            throw new PaymentGatewayException("Failed to initiate Remita payment", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        try
        {
            var hash = GenerateHash(transactionReference, 0);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization",
                $"remitaConsumerKey={_config.Remita.MerchantId},remitaConsumerToken={hash}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/exapp/api/v1/send/api/echannelsvc/{_config.Remita.MerchantId}/{transactionReference}/{hash}/orderstatus.reg");

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Remita verify response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Remita status: "01" = successful, "02" = processing, others = failed
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var amount = root.TryGetProperty("amount", out var am) ? am.GetDecimal() / 100m : 0m;

            var paymentStatus = status == "01" ? PaymentStatus.Successful
                : status == "02" ? PaymentStatus.Pending
                : PaymentStatus.Failed;

            return new VerificationResponse
            {
                Success = status == "01",
                TransactionReference = transactionReference,
                Amount = amount,
                Currency = "NGN",
                Status = paymentStatus,
                Message = paymentStatus == PaymentStatus.Successful ? "Payment verified successfully" : "Payment not completed",
                GatewayResponse = new Dictionary<string, string> { ["status"] = status ?? string.Empty }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Remita payment: {Ref}", transactionReference);
            throw new PaymentGatewayException("Failed to verify Remita payment", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        try
        {
            var hash = GenerateHash(request.TransactionReference, request.Amount);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization",
                $"remitaConsumerKey={_config.Remita.MerchantId},remitaConsumerToken={hash}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var payload = new
            {
                merchantId = _config.Remita.MerchantId,
                rrr = request.TransactionReference,
                amount = (long)(request.Amount * 100),
                reason = request.Reason ?? "Customer request"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/exapp/api/v1/send/api/echannelsvc/merchant/api/reversal", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Remita refund response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var statusCode = root.TryGetProperty("statuscode", out var sc) ? sc.GetString() : null;
            var isSuccess = statusCode == "025" || statusCode == "00";

            return new RefundResponse
            {
                Success = isSuccess,
                RefundReference = request.TransactionReference,
                TransactionReference = request.TransactionReference,
                Amount = request.Amount,
                Message = isSuccess ? "Refund initiated successfully" : "Refund failed",
                Status = isSuccess ? PaymentStatus.Refunded : PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Remita refund: {Ref}", request.TransactionReference);
            throw new PaymentGatewayException("Failed to process Remita refund", ex);
        }
    }
}
