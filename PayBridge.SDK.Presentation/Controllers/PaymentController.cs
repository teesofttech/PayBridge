using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Domain.Enums;

namespace PayBridge.SDK.Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new payment
    /// </summary>
    /// <param name="request">Payment details</param>
    /// <returns>Payment response with checkout URL</returns>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Creating payment for {Amount} {Currency}", request.Amount, request.Currency);

            var paymentRequest = MapToPaymentRequest(request);
            var paymentGateway = request.Gateway ?? PaymentGatewayType.Automatic;

            var response = await _paymentService.CreatePaymentAsync(paymentRequest, paymentGateway);

            if (response.Success)
            {
                _logger.LogInformation("Payment created successfully: {Reference}", response.TransactionReference);
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("Payment creation failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse
                {
                    Message = response.Message,
                    ErrorCode = "PAYMENT_FAILED"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment");
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing your payment",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Verifies a payment status
    /// </summary>
    /// <param name="reference">Transaction reference</param>
    /// <returns>Payment verification response</returns>
    [HttpGet]
    [ProducesResponseType(typeof(VerificationResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> VerifyPayment([FromQuery] VerifyRequest verifyRequest)
    {
        try
        {
            _logger.LogInformation("Verifying payment: {Reference}", verifyRequest.Reference);

            var response = await _paymentService.VerifyPaymentAsync(verifyRequest.Reference);

            if (response.Success)
            {
                _logger.LogInformation("Payment verification successful: {Reference}, Status: {Status}",
                    verifyRequest.Reference, response.Status);
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("Payment verification failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse
                {
                    Message = response.Message,
                    ErrorCode = "VERIFICATION_FAILED"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment: {Reference}", verifyRequest.Reference);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while verifying the payment",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Processes a refund
    /// </summary>
    /// <param name="request">Refund details</param>
    /// <returns>Refund response</returns>
    [HttpPost("refund")]
    [ProducesResponseType(typeof(RefundResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> RefundPayment([FromBody] RefundRequest request)
    {
        try
        {
            _logger.LogInformation("Processing refund for transaction: {Reference}", request.TransactionReference);

            var response = await _paymentService.RefundPaymentAsync(request);

            if (response.Success)
            {
                _logger.LogInformation("Refund processed successfully: {RefundReference}", response.RefundReference);
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("Refund processing failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse
                {
                    Message = response.Message,
                    ErrorCode = "REFUND_FAILED"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund: {Reference}", request.TransactionReference);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing the refund",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Handles payment webhook notifications
    /// </summary>
    /// <remarks>
    /// This endpoint processes webhook notifications from payment gateways.
    /// Each gateway sends notifications in different formats, so we need to
    /// detect the gateway from the notification format.
    /// </remarks>
    [HttpPost("webhook")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ProcessWebhook([FromBody] object webhookData)
    {
        try
        {
            _logger.LogInformation("Received webhook notification: {Data}", webhookData);

            // Determine which gateway sent the webhook
            PaymentGatewayType gateway = DetectGatewayFromWebhook(webhookData);
            string reference = ExtractReferenceFromWebhook(webhookData, gateway);

            if (string.IsNullOrEmpty(reference))
            {
                _logger.LogWarning("Could not extract transaction reference from webhook");
                return BadRequest(new ErrorResponse
                {
                    Message = "Could not extract transaction reference from webhook",
                    ErrorCode = "INVALID_WEBHOOK"
                });
            }

            // Verify the payment status
            var response = await _paymentService.VerifyPaymentAsync(reference, gateway);

            if (response.Success)
            {
                _logger.LogInformation("Webhook verification successful: {Reference}, Status: {Status}",
                    reference, response.Status);

                // TODO: Update order status or trigger other business logic based on payment status

                return Ok(new { success = true });
            }
            else
            {
                _logger.LogWarning("Webhook verification failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse
                {
                    Message = response.Message,
                    ErrorCode = "WEBHOOK_VERIFICATION_FAILED"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing the webhook",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Handles payment webhook notifications
    /// </summary>
    /// <remarks>
    /// This endpoint processes webhook notifications from payment gateways.
    /// Each gateway sends notifications in different formats, so we need to
    /// detect the gateway from the notification format.
    /// </remarks>
    [HttpGet("verify-transaction")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyTransaction()
    {
        try
        {
            var queryParams = HttpContext.Request.Query;

            var allParams = new Dictionary<string, string>();
            foreach (var param in queryParams)
            {
                if (param.Key.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
                    param.Key.Contains("tx_ref", StringComparison.OrdinalIgnoreCase))
                {
                    allParams["reference"] = param.Value!;
                }
            }

            PaymentGatewayType gateway = DetectGatewayFromWebhook(allParams.Values);
            string reference = allParams.Values.FirstOrDefault()!.ToString()!;

            if (string.IsNullOrEmpty(reference))
            {
                _logger.LogWarning("Could not extract transaction reference from Query");
                return BadRequest(new ErrorResponse
                {
                    Message = "Could not extract transaction reference from Query",
                    ErrorCode = "INVALID_REQUEST"
                });
            }

            // Verify the payment status
            var response = await _paymentService.VerifyPaymentAsync(reference, gateway);

            if (response.Success)
            {
                _logger.LogInformation("Query verification successful: {Reference}, Status: {Status}",
                    reference, response.Status);

                return Ok(new { success = true, response });
            }
            else
            {
                _logger.LogWarning("Query verification failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse
                {
                    Message = response.Message,
                    ErrorCode = "QUERY_VERIFICATION_FAILED"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Query");
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing the Query",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }


    /// <summary>
    /// Maps the API request DTO to the internal payment request
    /// </summary>
    private PaymentRequest MapToPaymentRequest(CreatePaymentRequest request)
    {
        return new PaymentRequest
        {
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            CustomerEmail = request.CustomerEmail,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            RedirectUrl = request.RedirectUrl,
            WebhookUrl = request.WebhookUrl,
            Metadata = request.Metadata,
            PaymentMethodType = request.PaymentMethodType,
            SavedPaymentMethodId = request.SavedPaymentMethodId
        };
    }

    /// <summary>
    /// Detects which payment gateway sent the webhook notification
    /// based on the structure of the webhook data
    /// </summary>
    private PaymentGatewayType DetectGatewayFromWebhook(object webhookData)
    {
        // Convert to dynamic to inspect properties
        dynamic data = webhookData;

        try
        {
            // Paystack webhooks contain an 'event' property
            if (((IDictionary<string, object>)data).ContainsKey("event"))
            {
                return PaymentGatewayType.Paystack;
            }

            // Flutterwave webhooks contain a 'flw_ref' property
            if (((IDictionary<string, object>)data).ContainsKey("flw_ref"))
            {
                return PaymentGatewayType.Flutterwave;
            }

            // Stripe webhooks contain a 'type' property starting with 'stripe.'
            if (((IDictionary<string, object>)data).ContainsKey("type") &&
                data.type.ToString().StartsWith("stripe."))
            {
                return PaymentGatewayType.Stripe;
            }

            // Checkout.com webhooks contain a '_links' property
            if (((IDictionary<string, object>)data).ContainsKey("_links"))
            {
                return PaymentGatewayType.Checkout;
            }

            // Default to Automatic if we can't determine the gateway
            return PaymentGatewayType.Automatic;
        }
        catch
        {
            // If we encounter any errors, default to Automatic
            return PaymentGatewayType.Automatic;
        }
    }

    /// <summary>
    /// Extracts the transaction reference from the webhook notification
    /// based on the gateway that sent it
    /// </summary>
    private string ExtractReferenceFromWebhook(object webhookData, PaymentGatewayType gateway)
    {
        // Convert to dynamic to inspect properties
        dynamic data = webhookData;

        try
        {
            switch (gateway)
            {
                case PaymentGatewayType.Paystack:
                    // Paystack webhook data is nested in a 'data' property
                    return data.data.reference;

                case PaymentGatewayType.Flutterwave:
                    // Flutterwave uses 'tx_ref' for the merchant reference
                    return data.tx_ref;

                case PaymentGatewayType.Stripe:
                    // Stripe's webhook data depends on the event type
                    if (data.type.ToString().StartsWith("payment_intent."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    // For charge events
                    else if (data.type.ToString().StartsWith("charge."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    break;

                case PaymentGatewayType.Checkout:
                    // Checkout.com webhook format
                    return data.data.reference;
            }

            // If we couldn't extract a reference using the known formats,
            // try some common property names
            var dict = (IDictionary<string, object>)data;

            foreach (var prop in new[] { "reference", "transaction_reference", "txn_ref", "id" })
            {
                if (dict.ContainsKey(prop))
                {
                    return dict[prop].ToString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetKeyAndValue(object data, string key)
    {
        var dict = (IDictionary<string, object>)data;
        if (dict.ContainsKey(key))
        {
            return dict[key].ToString();
        }
        return null;
    }
}

/// <summary>
/// API request DTO for creating a payment
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Amount to be paid
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., USD, NGN, EUR)
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Description of the payment
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Email address of the customer
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Name of the customer
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Phone number of the customer
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// URL to redirect the customer to after payment
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// URL for webhook notifications
    /// </summary>
    public string WebhookUrl { get; set; }

    /// <summary>
    /// Additional metadata for the transaction
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Type of payment method to use
    /// </summary>
    public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Card;

    /// <summary>
    /// ID of a previously saved payment method
    /// </summary>
    public string SavedPaymentMethodId { get; set; }

    /// <summary>
    /// Specific gateway to use, or null for automatic selection
    /// </summary>
    public PaymentGatewayType? Gateway { get; set; }
}

/// <summary>
/// API request DTO for saving a payment method
/// </summary>
public class SavePaymentMethodRequest
{
    /// <summary>
    /// Email address of the customer
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Name of the customer
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Token from the gateway's client-side SDK
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Type of payment method
    /// </summary>
    public PaymentMethodType Type { get; set; } = PaymentMethodType.Card;

    /// <summary>
    /// Whether this is the customer's default payment method
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Specific gateway to use (cannot be Automatic)
    /// </summary>
    public PaymentGatewayType Gateway { get; set; }
}

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Machine-readable error code
    /// </summary>
    public string ErrorCode { get; set; }
}

