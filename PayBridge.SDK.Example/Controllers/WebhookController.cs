using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using PayBridge.SDK.Helper;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// SCENARIO 4 — Receiving and processing gateway webhook notifications.
///
/// Every payment gateway sends a server-to-server POST to the URL you supplied
/// as <c>webhookUrl</c> in the payment request. This controller demonstrates:
///
/// 1. How to receive the raw webhook body.
/// 2. How to detect which gateway sent it (using <see cref="GatewayExtractor"/>).
/// 3. How to extract the transaction reference.
/// 4. How to call <c>IPaymentService.VerifyPaymentAsync</c> to confirm status.
/// 5. How to update your business logic (order fulfilment) on confirmation.
///
/// LOCAL DEVELOPMENT TIP
/// ─────────────────────
/// Gateways cannot POST to localhost. Use a tunnel tool to expose your local
/// machine to the internet:
///
///   • ngrok:        ngrok http 7252
///   • localtunnel:  lt --port 7252
///
/// Then supply the generated URL as <c>webhookUrl</c> when creating a payment,
/// e.g. "https://abcd1234.ngrok.io/api/webhook".
/// </summary>
[ApiController]
[Route("api/webhook")]
[Produces("application/json")]
public class WebhookController : ControllerBase
{
    private readonly IPaymentService _payment;
    private readonly OrderService    _orders;
    private readonly ILogger<WebhookController> _log;

    public WebhookController(
        IPaymentService paymentService,
        OrderService    orderService,
        ILogger<WebhookController> logger)
    {
        _payment = paymentService;
        _orders  = orderService;
        _log     = logger;
    }

    // ── POST /api/webhook ────────────────────────────────────────────────────

    /// <summary>
    /// Receives a webhook notification from any supported payment gateway.
    ///
    /// The SDK auto-detects the gateway from the webhook body structure, so you
    /// don't need a separate endpoint per gateway.
    ///
    /// **Important:** Always return HTTP 200 quickly — gateways will retry if
    /// they don't receive a timely 2xx response. Heavy processing should be
    /// offloaded to a background queue in production.
    /// </summary>
    /// <remarks>
    /// The body format varies per gateway. Some examples:
    ///
    /// **Paystack** — contains an "event" key:
    ///
    ///     { "event": "charge.success", "data": { "reference": "TXN_xxx" } }
    ///
    /// **Flutterwave** — contains "flw_ref":
    ///
    ///     { "event": "charge.completed", "flw_ref": "FLW_xxx", "tx_ref": "TXN_xxx" }
    ///
    /// **Stripe** — "type" starts with "payment_intent.":
    ///
    ///     { "type": "payment_intent.succeeded", "data": { "object": { ... } } }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive([FromBody] object webhookBody)
    {
        _log.LogInformation("Webhook received");

        // ── Step 1: detect which gateway sent this ────────────────────────────
        //
        // GatewayExtractor inspects well-known webhook fingerprints (e.g. the
        // presence of an "event" key for Paystack, "flw_ref" for Flutterwave).
        var gateway = GatewayExtractor.DetectGatewayFromWebhook(webhookBody);

        _log.LogInformation("Detected gateway: {Gateway}", gateway);

        // ── Step 2: extract the transaction reference ─────────────────────────
        var reference = GatewayExtractor.ExtractReferenceFromWebhook(webhookBody, gateway);

        if (string.IsNullOrWhiteSpace(reference))
        {
            _log.LogWarning("Could not extract transaction reference from webhook body");
            // Return 200 anyway — returning 4xx can trigger gateway retries
            return Ok(new { received = true, processed = false, reason = "no_reference" });
        }

        _log.LogInformation("Transaction reference: {Reference}", reference);

        // ── Step 3: verify the payment with the gateway ───────────────────────
        //
        // NEVER trust the webhook body alone — always call VerifyPaymentAsync to
        // confirm the transaction status server-side before fulfilling the order.
        var verification = await _payment.VerifyPaymentAsync(reference, gateway);

        if (!verification.Success || verification.Status != PaymentStatus.Successful)
        {
            _log.LogWarning(
                "Webhook for {Reference} — verification failed or status={Status}",
                reference, verification.Status);

            return Ok(new { received = true, processed = false, status = verification.Status.ToString() });
        }

        // ── Step 4: fulfil the order ──────────────────────────────────────────
        //
        // Look up the order using the orderId we embedded in Metadata, then mark
        // it as paid. In a real app you'd:
        //   • Send a confirmation email
        //   • Provision the purchased product / subscription
        //   • Emit a domain event for downstream services
        var orderId = verification.Metadata.GetValueOrDefault("orderId")
                   ?? _orders.GetByTransactionRef(reference)?.OrderId;

        if (orderId is not null)
        {
            _orders.MarkAsPaid(orderId, reference);
            _log.LogInformation("Order {OrderId} marked as paid ✓", orderId);
        }
        else
        {
            _log.LogWarning(
                "Verified payment {Reference} but could not find a matching order",
                reference);
        }

        // ── Step 5: return 200 immediately ────────────────────────────────────
        return Ok(new { received = true, processed = true, reference, gateway = gateway.ToString() });
    }
}
