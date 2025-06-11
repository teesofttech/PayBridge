using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Helper;
using PayBridge.SDK.Interfaces;
using PayBridge.SDK.Presentation.Mapper;
using PayBridge.SDK.Presentation.Models;


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

            var paymentRequest = PaymentRequestMapper.MapToPaymentRequest(request);
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

            PaymentGatewayType gateway = GatewayExtractor.DetectGatewayFromWebhook(allParams.Values);
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
            PaymentGatewayType gateway = GatewayExtractor.DetectGatewayFromWebhook(webhookData);
            string reference = GatewayExtractor.ExtractReferenceFromWebhook(webhookData, gateway);

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
}

