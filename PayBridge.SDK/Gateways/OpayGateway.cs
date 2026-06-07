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

namespace PayBridge.SDK;

/// <summary>
/// OPay payment gateway implementation (Nigeria / Africa).
/// Supports card, bank transfer, USSD, and mobile money payments.
/// </summary>
public class OpayGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpayGateway> _logger;

    private const string LiveBaseUrl = "https://cashierapi.opayweb.com/api/v3";
    private const string SandboxBaseUrl = "https://sandboxapi.opaycheckout.com/api/v3";
    private const string TxRefPrefix = "OP_";

    public OpayGateway(
        PaymentGatewayConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<OpayGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClientFactory.CreateClient(nameof(OpayGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.Opay;

    private string BaseUrl => _config.Opay.IsSandbox ? SandboxBaseUrl : LiveBaseUrl;

    /// <summary>
    /// Generates HMAC-SHA512 signature: HMAC512(jsonBody, secretKey)
    /// </summary>
    private string GenerateSignature(string jsonBody)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_config.Opay.SecretKey);
        var msgBytes = Encoding.UTF8.GetBytes(jsonBody);
        var hash = HMACSHA512.HashData(keyBytes, msgBytes);
        return Convert.ToHexString(hash).ToLower();
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var txRef = $"{TxRefPrefix}{Guid.NewGuid():N}";
            var amountKobo = (long)(request.Amount * 100);

            var payload = new
            {
                merchantId = _config.Opay.MerchantId,
                reference = txRef,
                amount = new { total = amountKobo, currency = request.Currency ?? "NGN" },
                returnUrl = request.RedirectUrl ?? string.Empty,
                callbackUrl = request.WebhookUrl ?? string.Empty,
                cancelUrl = request.RedirectUrl ?? string.Empty,
                expireAt = 30,
                userInfo = new
                {
                    userEmail = request.CustomerEmail,
                    userName = request.CustomerName ?? string.Empty,
                    userMobile = request.CustomerPhone ?? string.Empty
                },
                product = new
                {
                    name = request.Description ?? "Payment",
                    description = request.Description ?? "Payment"
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var signature = GenerateSignature(json);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_config.Opay.PublicKey}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("MerchantId", _config.Opay.MerchantId);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Signature", signature);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/international/cashier/create", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("OPay create response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;

            if (code != "00000")
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"OPay payment initiation failed: {msg}",
                    Status = PaymentStatus.Failed
                };
            }

            var data = root.GetProperty("data");
            var cashierUrl = data.TryGetProperty("cashierUrl", out var cu) ? cu.GetString() : string.Empty;

            return new PaymentResponse
            {
                Success = true,
                TransactionReference = txRef,
                CheckoutUrl = cashierUrl ?? string.Empty,
                Message = "Payment initiated successfully",
                Status = PaymentStatus.Pending,
                GatewayResponse = new Dictionary<string, string>
                {
                    ["reference"] = txRef,
                    ["cashierUrl"] = cashierUrl ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating OPay payment");
            throw new PaymentGatewayException("Failed to initiate OPay payment", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        try
        {
            var payload = new
            {
                merchantId = _config.Opay.MerchantId,
                reference = transactionReference
            };

            var json = JsonSerializer.Serialize(payload);
            var signature = GenerateSignature(json);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_config.Opay.PublicKey}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("MerchantId", _config.Opay.MerchantId);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Signature", signature);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/international/cashier/query", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("OPay verify response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var data = code == "00000" && root.TryGetProperty("data", out var d) ? d : (JsonElement?)null;

            // OPay statuses: SUCCESS, PENDING, FAIL, CLOSE
            var status = data?.TryGetProperty("status", out var st) == true ? st.GetString() : null;
            var amountKobo = data?.TryGetProperty("amount", out var am) == true ? am.GetInt64() : 0L;
            var currency = data?.TryGetProperty("currency", out var cur) == true ? cur.GetString() : "NGN";

            var paymentStatus = status == "SUCCESS" ? PaymentStatus.Successful
                : status == "PENDING" ? PaymentStatus.Pending
                : PaymentStatus.Failed;

            return new VerificationResponse
            {
                Success = status == "SUCCESS",
                TransactionReference = transactionReference,
                Amount = amountKobo / 100m,
                Currency = currency ?? "NGN",
                Status = paymentStatus,
                Message = status == "SUCCESS" ? "Payment verified successfully" : $"Payment status: {status}",
                GatewayResponse = new Dictionary<string, string> { ["status"] = status ?? string.Empty }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OPay payment: {Ref}", transactionReference);
            throw new PaymentGatewayException("Failed to verify OPay payment", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        try
        {
            var payload = new
            {
                merchantId = _config.Opay.MerchantId,
                reference = request.TransactionReference,
                amount = new { total = (long)(request.Amount * 100), currency = "NGN" },
                reason = request.Reason ?? "Customer request"
            };

            var json = JsonSerializer.Serialize(payload);
            var signature = GenerateSignature(json);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_config.Opay.PublicKey}");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("MerchantId", _config.Opay.MerchantId);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Signature", signature);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/international/cashier/refund", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("OPay refund response: {Body}", responseBody);

            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var isSuccess = code == "00000";
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;

            return new RefundResponse
            {
                Success = isSuccess,
                RefundReference = request.TransactionReference,
                TransactionReference = request.TransactionReference,
                Amount = request.Amount,
                Message = isSuccess ? "Refund initiated successfully" : (msg ?? "Refund failed"),
                Status = isSuccess ? PaymentStatus.Refunded : PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OPay refund: {Ref}", request.TransactionReference);
            throw new PaymentGatewayException("Failed to process OPay refund", ex);
        }
    }
}
