using System.Text;
using System.Xml.Linq;
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
/// DPO Group payment gateway implementation (Africa - 19+ countries).
/// Supports KES, GHS, UGX, TZS, ZAR, RWF, USD, EUR and more.
/// Uses XML-based API v6 at https://secure.3gdirectpay.com/API/v6/
/// </summary>
public class DpoGroupGateway : IPaymentGateway
{
    private readonly PaymentGatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DpoGroupGateway> _logger;

    private const string LiveApiUrl = "https://secure.3gdirectpay.com/API/v6/";
    private const string SandboxApiUrl = "https://secure.3gdirectpay.com/API/v6/";
    private const string LivePayUrl = "https://secure.3gdirectpay.com/payv2.php";
    private const string SandboxPayUrl = "https://secure.3gdirectpay.com/payv2.php";
    private const string TxRefPrefix = "DPO_";

    public DpoGroupGateway(
        PaymentGatewayConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<DpoGroupGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClientFactory.CreateClient(nameof(DpoGroupGateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PaymentGatewayType GatewayType => PaymentGatewayType.DpoGroup;

    private string ApiUrl => _config.DpoGroup.IsSandbox ? SandboxApiUrl : LiveApiUrl;
    private string PayUrl => _config.DpoGroup.IsSandbox ? SandboxPayUrl : LivePayUrl;

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request)
    {
        try
        {
            var txRef = $"{TxRefPrefix}{Guid.NewGuid():N}";
            var currency = request.Currency ?? _config.DpoGroup.PaymentCurrency;
            var amount = request.Amount.ToString("F2");

            // Build XML payload for createToken
            var xml = new XDocument(
                new XElement("API3G",
                    new XElement("CompanyToken", _config.DpoGroup.CompanyToken),
                    new XElement("Request", "createToken"),
                    new XElement("Transaction",
                        new XElement("PaymentAmount", amount),
                        new XElement("PaymentCurrency", currency),
                        new XElement("CompanyRef", txRef),
                        new XElement("RedirectURL", request.RedirectUrl ?? string.Empty),
                        new XElement("BackURL", request.RedirectUrl ?? string.Empty),
                        new XElement("CompanyRefUnique", "0"),
                        new XElement("PTL", "5")
                    ),
                    new XElement("Services",
                        new XElement("Service",
                            new XElement("ServiceType", "3854"),
                            new XElement("ServiceDescription", request.Description ?? "Payment"),
                            new XElement("ServiceDate", DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm"))
                        )
                    )
                )
            );

            var xmlString = xml.ToString();
            _logger.LogDebug("DPO createToken request prepared for CompanyRef: {CompanyRef}", txRef);
            var content = new StringContent(xmlString, Encoding.UTF8, "application/xml");
            var response = await _httpClient.PostAsync(ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("DPO createToken response: {Body}", responseBody);

            var doc = XDocument.Parse(responseBody);
            var result = doc.Root?.Element("Result")?.Value;
            var resultExplanation = doc.Root?.Element("ResultExplanation")?.Value;

            if (result != "000")
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = $"DPO token creation failed: {resultExplanation}",
                    Status = PaymentStatus.Failed
                };
            }

            var transToken = doc.Root?.Element("TransToken")?.Value ?? string.Empty;
            var checkoutUrl = $"{PayUrl}?ID={transToken}";

            return new PaymentResponse
            {
                Success = true,
                TransactionReference = txRef,
                CheckoutUrl = checkoutUrl,
                Message = "Payment initiated successfully",
                Status = PaymentStatus.Pending,
                GatewayResponse = new Dictionary<string, string>
                {
                    ["transToken"] = transToken,
                    ["companyRef"] = txRef,
                    ["checkoutUrl"] = checkoutUrl
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating DPO Group payment");
            throw new PaymentGatewayException("Failed to initiate DPO Group payment", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference)
    {
        try
        {
            // For DPO, transactionReference is the TransToken returned at creation
            // Strip DPO_ prefix if present to get the companyRef, but we need the TransToken
            // Convention: we store the TransToken as the gateway's transaction reference
            var xml = new XDocument(
                new XElement("API3G",
                    new XElement("CompanyToken", _config.DpoGroup.CompanyToken),
                    new XElement("Request", "verifyToken"),
                    new XElement("TransactionToken", transactionReference)
                )
            );

            var xmlString = xml.ToString();
            var content = new StringContent(xmlString, Encoding.UTF8, "application/xml");
            var response = await _httpClient.PostAsync(ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("DPO verifyToken response: {Body}", responseBody);

            var doc = XDocument.Parse(responseBody);
            var result = doc.Root?.Element("Result")?.Value;
            var resultExplanation = doc.Root?.Element("ResultExplanation")?.Value;
            var customerName = doc.Root?.Element("CustomerName")?.Value ?? string.Empty;
            var customerEmail = doc.Root?.Element("CustomerEmail")?.Value ?? string.Empty;
            var transactionAmountStr = doc.Root?.Element("TransactionAmount")?.Value ?? "0";
            var transactionCurrency = doc.Root?.Element("TransactionCurrency")?.Value ?? _config.DpoGroup.PaymentCurrency;
            var companyRef = doc.Root?.Element("CompanyRef")?.Value ?? transactionReference;

            decimal.TryParse(transactionAmountStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var transactionAmount);

            // DPO result codes: "000" = payment verified successfully
            var paymentStatus = result == "000" ? PaymentStatus.Successful
                : result == "001" ? PaymentStatus.Pending
                : PaymentStatus.Failed;

            return new VerificationResponse
            {
                Success = result == "000",
                TransactionReference = companyRef,
                Amount = transactionAmount,
                Currency = transactionCurrency,
                Status = paymentStatus,
                Message = result == "000" ? "Payment verified successfully" : (resultExplanation ?? $"Payment status code: {result}"),
                GatewayResponse = new Dictionary<string, string>
                {
                    ["result"] = result ?? string.Empty,
                    ["resultExplanation"] = resultExplanation ?? string.Empty,
                    ["customerName"] = customerName,
                    ["customerEmail"] = customerEmail
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying DPO Group payment: {Ref}", transactionReference);
            throw new PaymentGatewayException("Failed to verify DPO Group payment", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        try
        {
            // DPO Group refunds are initiated via refundToken request
            var xml = new XDocument(
                new XElement("API3G",
                    new XElement("CompanyToken", _config.DpoGroup.CompanyToken),
                    new XElement("Request", "refundToken"),
                    new XElement("TransactionToken", request.TransactionReference),
                    new XElement("refundAmount", request.Amount.ToString("F2")),
                    new XElement("refundDetails", request.Reason ?? "Customer request")
                )
            );

            var xmlString = xml.ToString();
            var content = new StringContent(xmlString, Encoding.UTF8, "application/xml");
            var response = await _httpClient.PostAsync(ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("DPO refundToken response: {Body}", responseBody);

            var doc = XDocument.Parse(responseBody);
            var result = doc.Root?.Element("Result")?.Value;
            var resultExplanation = doc.Root?.Element("ResultExplanation")?.Value;
            var isSuccess = result == "000";

            return new RefundResponse
            {
                Success = isSuccess,
                RefundReference = request.TransactionReference,
                TransactionReference = request.TransactionReference,
                Amount = request.Amount,
                Message = isSuccess ? "Refund initiated successfully" : (resultExplanation ?? "Refund failed"),
                Status = isSuccess ? PaymentStatus.Refunded : PaymentStatus.Failed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing DPO Group refund: {Ref}", request.TransactionReference);
            throw new PaymentGatewayException("Failed to process DPO Group refund", ex);
        }
    }
}
