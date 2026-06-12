using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Mapper;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// SCENARIO 1 — Explicit gateway selection.
/// SCENARIO 2 — Automatic gateway selection.
///
/// These two scenarios share one controller because the code path is almost
/// identical; the only difference is the <c>Gateway</c> field in the request.
///
/// Scenario 1 (explicit):  set <c>Gateway = Paystack</c> (or any other value).
/// Scenario 2 (automatic): omit <c>Gateway</c> or set it to <c>Automatic (0)</c>.
///   The SDK then picks the best gateway based on the currency / region.
/// </summary>
[ApiController]
[Route("api/payment")]
[Produces("application/json")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService  _payment;
    private readonly OrderService     _orders;
    private readonly ILogger<PaymentController> _log;

    public PaymentController(
        IPaymentService  paymentService,
        OrderService     orderService,
        ILogger<PaymentController> logger)
    {
        _payment = paymentService;
        _orders  = orderService;
        _log     = logger;
    }

    // ── POST /api/payment ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a hosted-checkout payment and returns the gateway's checkout URL.
    ///
    /// **Scenario 1 — Explicit gateway:**
    /// Set <c>gateway</c> to a specific value (e.g. 2 = Paystack, 1 = Flutterwave,
    /// 3 = Stripe). The SDK uses that gateway regardless of currency.
    ///
    /// **Scenario 2 — Automatic gateway:**
    /// Omit <c>gateway</c> (or send 0). The SDK inspects the <c>currency</c> field
    /// and picks the most appropriate active gateway automatically.
    ///
    /// On success redirect your customer to <c>data.checkoutUrl</c>.
    /// </summary>
    /// <remarks>
    /// Sample request (Scenario 1 — Paystack, NGN):
    ///
    ///     POST /api/payment
    ///     {
    ///         "amount": 5000,
    ///         "currency": "NGN",
    ///         "description": "Order #1042",
    ///         "customerEmail": "jane@example.com",
    ///         "customerName": "Jane Doe",
    ///         "redirectUrl": "https://localhost:7252/api/verify",
    ///         "webhookUrl": "https://your-ngrok-url/api/webhook",
    ///         "metadata": { "orderId": "ORD-1042" },
    ///         "gateway": 2
    ///     }
    ///
    /// Sample request (Scenario 2 — Automatic, USD falls back to Stripe):
    ///
    ///     POST /api/payment
    ///     {
    ///         "amount": 1999,
    ///         "currency": "USD",
    ///         "description": "Pro plan — monthly",
    ///         "customerEmail": "jane@example.com",
    ///         "customerName": "Jane Doe",
    ///         "redirectUrl": "https://localhost:7252/api/verify",
    ///         "webhookUrl": "https://your-ngrok-url/api/webhook"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePayment([FromBody] CheckoutRequest request)
    {
        // ── Step 1: create an order in our in-memory store ───────────────────
        //
        // In a real app you'd persist this to a database so the webhook handler
        // can look it up later using the metadata orderId.
        var order = _orders.Create(
            request.CustomerEmail,
            request.Amount,
            request.Currency,
            request.Description);

        _log.LogInformation(
            "Order {OrderId} created — initiating payment via {Gateway}",
            order.OrderId,
            request.Gateway?.ToString() ?? "Automatic");

        // ── Step 2: attach the orderId to metadata so it comes back in the webhook
        request.Metadata["orderId"] = order.OrderId;

        // ── Step 3: map our request model to the SDK's PaymentRequest ─────────
        var sdkRequest = CheckoutRequestMapper.ToPaymentRequest(request);

        // ── Step 4: pick gateway ──────────────────────────────────────────────
        //
        // SCENARIO 1: request.Gateway is a specific enum value  → use it directly
        // SCENARIO 2: request.Gateway is null / Automatic       → SDK auto-selects
        var gateway = request.Gateway ?? PaymentGatewayType.Automatic;

        try
        {
            var response = await _payment.CreatePaymentAsync(sdkRequest, gateway);

            if (!response.Success)
            {
                _log.LogWarning("Payment creation failed for order {OrderId}: {Msg}", order.OrderId, response.Message);
                _orders.MarkAsFailed(order.OrderId);

                return BadRequest(ApiResponse<object>.Fail(response.Message, "PAYMENT_FAILED"));
            }

            _log.LogInformation(
                "Checkout URL issued for order {OrderId} — ref {Ref}",
                order.OrderId, response.TransactionReference);

            return Ok(ApiResponse<object>.Ok(new
            {
                orderId              = order.OrderId,
                transactionReference = response.TransactionReference,
                checkoutUrl          = response.CheckoutUrl,
                gateway              = gateway.ToString(),
                message              = response.Message,
            }));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error creating payment for order {OrderId}", order.OrderId);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An unexpected error occurred. Check the server logs.",
                "INTERNAL_ERROR"));
        }
    }
}
