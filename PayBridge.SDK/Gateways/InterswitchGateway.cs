using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PayBridge.SDK;

/// <summary>
/// Interswitch payment gateway — Nigeria (Quickteller / Webpay).
/// The largest Nigerian fintech network supporting ATM, POS, web, and USSD.
/// Docs: https://developer.interswitchgroup.com/
/// </summary>
public class InterswitchGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<InterswitchGateway> _logger;

    private const string LiveUrl = "https://api.interswitchgroup.com";
    private const string SandboxUrl = "https://sandbox.interswitchng.com";

    public PaymentGatewayType GatewayType => PaymentGatewayType.Interswitch;

    public InterswitchGateway(IOptions<PaymentGatewayConfig> config, ILogger<InterswitchGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_config.Interswitch.ClientId))
            throw new InvalidOperationException("Interswitch ClientId is required");

        if (string.IsNullOrEmpty(_config.Interswitch.ClientSecret))
            throw new InvalidOperationException("Interswitch ClientSecret is required");

        if (string.IsNullOrEmpty(_config.Interswitch.MerchantCode))
            throw new InvalidOperationException("Interswitch MerchantCode is required");

        var baseUrl = _config.Interswitch.IsSandbox ? SandboxUrl : LiveUrl;

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // -----------------------------------------------------------------------
    // Interswitch uses OAuth 2.0 client_credentials flow for access tokens.
    // -----------------------------------------------------------------------

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config.Interswitch.ClientId}:{_config.Interswitch.ClientSecret}"));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/passport/oauth/token");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "profile"
        });

var response = await _httpClient.SendAsync(tokenRequest);
var body = await response.Content.ReadAsStringAsync();

if (response.IsSuccessStatusCode)
{
    _logger.LogDebug("Interswitch auth succeeded ({StatusCode})", response.StatusCode);
}
else
{
    _logger.LogDebug("Interswitch auth failed ({StatusCode}): {Body}", response.StatusCode, body);
}

using var doc = JsonDocument.Parse(body);
var root = doc.RootElement;

        if (response.IsSuccessStatusCode && root.TryGetProperty("access_token", out var token))
            return token.GetString() ?? throw new InvalidOperationException("Interswitch returned an empty access token");

        throw new InvalidOperationException("Failed to obtain Interswitch access token");
    }

    // -----------------------------------------------------------------------
// Interswitch requires HMAC-SHA512 request signing for Webpay.
// Current implementation: Base64(HMACSHA512(key=clientSecret, message=requestRef + timestamp + clientId + clientSecret))

    private static string GenerateSignature(string requestRef, string timestamp, string clientId, string clientSecret)
    {
        var rawSignature = $"{requestRef}{timestamp}{clientId}{clientSecret}";
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        _logger.LogInformation("Creating Interswitch payment for customer {Email}", request.CustomerEmail);

        try
        {
            var accessToken = await GetAccessTokenAsync();
            var txRef = $"ISW_{Guid.NewGuid():N}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = GenerateSignature(txRef, timestamp, _config.Interswitch.ClientId, _config.Interswitch.ClientSecret);

            var iswRequest = new
            {
                merchantCode = _config.Interswitch.MerchantCode,
                payableCode = _config.Interswitch.PaymentItemCode,
                merchantCustomerName = request.CustomerName,
                merchantCustomerEmail = request.CustomerEmail,
                merchantCustomerPhone = request.CustomerPhone,
                amount = (int)(request.Amount * 100), // in kobo
                currencyCode = "566", // ISO 4217 numeric code for NGN
                transactionReference = txRef,
                redirectUrl = request.RedirectUrl,
                requestType = "1" // 1 = redirect / hosted page
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/purchases");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Add("Signature", signature);
            httpRequest.Headers.Add("Timestamp", timestamp);
            httpRequest.Headers.Add("Nonce", Guid.NewGuid().ToString("N"));
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(iswRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Interswitch create payment response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode && root.TryGetProperty("redirectUrl", out var checkoutUrl))
            {
                return new PaymentResponse
                {
                    Success = true,
                    TransactionReference = txRef,
                    Message = "Interswitch payment initiated successfully",
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = checkoutUrl.GetString() ?? string.Empty,
                    GatewayResponse = new Dictionary<string, string>
                    {
                        { "transaction_reference", txRef }
                    }
                };
            }

            var errorMessage = root.TryGetProperty("responseDescription", out var desc)
                ? desc.GetString() ?? "Unknown error"
                : "Unknown error occurred";

            _logger.LogError("Interswitch payment initiation failed: {Error}", errorMessage);
            return new PaymentResponse { Success = false, Message = errorMessage, Status = PaymentStatus.Failed };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Interswitch payment");
            throw new Exception("Failed to create payment with Interswitch", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        _logger.LogInformation("Verifying Interswitch payment: {Reference}", transactionReference);

        try
        {
            var accessToken = await GetAccessTokenAsync();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = GenerateSignature(transactionReference, timestamp, _config.Interswitch.ClientId, _config.Interswitch.ClientSecret);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v2/purchases?merchantCode={_config.Interswitch.MerchantCode}&transactionReference={transactionReference}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Add("Signature", signature);
            httpRequest.Headers.Add("Timestamp", timestamp);
            httpRequest.Headers.Add("Nonce", Guid.NewGuid().ToString("N"));

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Interswitch verify response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            if (response.IsSuccessStatusCode)
            {
                var responseCode = root.TryGetProperty("responseCode", out var rc)
                    ? rc.GetString() ?? string.Empty
                    : string.Empty;

                // Interswitch response codes: 00 = success, others = failure/pending
                var paymentStatus = responseCode switch
                {
                    "00" => PaymentStatus.Successful,
                    "T0" => PaymentStatus.Pending, // transaction pending
                    _ => PaymentStatus.Failed
                };

                return new VerificationResponse
                {
                    Success = true,
                    TransactionReference = transactionReference,
                    Message = root.TryGetProperty("responseDescription", out var desc)
                        ? desc.GetString() ?? "Verification complete"
                        : "Verification complete",
                    Status = paymentStatus,
                    Amount = root.TryGetProperty("amount", out var amount)
                        ? amount.GetDecimal() / 100
                        : 0,
                    Currency = "NGN",
                    PaymentDate = root.TryGetProperty("transactionDate", out var txDate)
                        && !string.IsNullOrEmpty(txDate.GetString())
                        ? DateTime.Parse(txDate.GetString()!)
                        : DateTime.UtcNow,
                    PaymentMethod = root.TryGetProperty("paymentChannelName", out var channel)
                        ? channel.GetString() ?? string.Empty
                        : string.Empty
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
            _logger.LogError(ex, "Error verifying Interswitch payment");
            throw new Exception("Failed to verify payment with Interswitch", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        _logger.LogInformation("Processing Interswitch refund for: {Reference}", request.TransactionReference);

        try
        {
            var accessToken = await GetAccessTokenAsync();
            var refundRef = $"REF_{Guid.NewGuid():N}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = GenerateSignature(refundRef, timestamp, _config.Interswitch.ClientId, _config.Interswitch.ClientSecret);

            var refundRequest = new
            {
                merchantCode = _config.Interswitch.MerchantCode,
                transactionReference = request.TransactionReference,
                refundReference = refundRef,
                amount = (int)(request.Amount * 100),
                currencyCode = "566",
                remarks = request.Reason ?? "Customer request"
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/refunds");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Add("Signature", signature);
            httpRequest.Headers.Add("Timestamp", timestamp);
            httpRequest.Headers.Add("Nonce", Guid.NewGuid().ToString("N"));
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(refundRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Interswitch refund response: {Response}", responseBody);

            using var jsonDocument = JsonDocument.Parse(responseBody);
            var root = jsonDocument.RootElement;

            var responseCode = root.TryGetProperty("responseCode", out var rc)
                ? rc.GetString() ?? string.Empty
                : string.Empty;

            if (response.IsSuccessStatusCode && responseCode == "00")
            {
                return new RefundResponse
                {
                    Success = true,
                    RefundReference = refundRef,
                    TransactionReference = request.TransactionReference,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    Message = "Refund processed successfully",
                    RefundDate = DateTime.UtcNow
                };
            }

            var errorMessage = root.TryGetProperty("responseDescription", out var desc)
                ? desc.GetString() ?? "Refund failed"
                : "Refund request failed";

            return new RefundResponse { Success = false, Message = errorMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Interswitch refund");
            throw new Exception("Failed to process refund with Interswitch", ex);
        }
    }
}
