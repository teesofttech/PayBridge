using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using PayBridge.SDK.Helper;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// SCENARIO 5 — Redirect-back verification (the gateway callback).
///
/// After the customer completes (or abandons) payment on the hosted checkout
/// page, the gateway redirects them back to the <c>redirectUrl</c> you provided
/// when initiating the payment. This controller receives that redirect.
///
/// The reference is usually passed as a query-string parameter, but the exact
/// key name varies by gateway:
///
///   • Paystack     → ?reference=TXN_xxx
///   • Flutterwave  → ?tx_ref=TXN_xxx
///   • Stripe       → ?session_id=cs_xxx  (Stripe Checkout)
///   • Korapay      → ?reference=KPY-xxx
///   • Monnify      → ?paymentReference=MNFY_xxx
///
/// This controller normalises all those variants, calls VerifyPaymentAsync, and
/// returns a clean JSON result.
/// </summary>
[ApiController]
[Route("api/verify")]
[Produces("application/json")]
public class VerifyController : ControllerBase
{
    private readonly IPaymentService _payment;
    private readonly OrderService    _orders;
    private readonly ILogger<VerifyController> _log;

    public VerifyController(
        IPaymentService paymentService,
        OrderService    orderService,
        ILogger<VerifyController> logger)
    {
        _payment = paymentService;
        _orders  = orderService;
        _log     = logger;
    }

    // ── GET /api/verify ───────────────────────────────────────────────────────

    /// <summary>
    /// Handles the gateway redirect after checkout.
    ///
    /// Accepts any of the following query-string keys to find the transaction:
    /// <c>reference</c>, <c>tx_ref</c>, <c>paymentReference</c>, <c>session_id</c>.
    ///
    /// Returns the full verification result so you can:
    /// - Display a "Payment successful" page (redirect if needed).
    /// - Show an error page on failure.
    /// - Trigger order fulfilment in cases where the webhook was not received.
    ///
    /// **Important:** Do not fulfil orders here as your only mechanism — a
    /// determined user can hit this URL without actually paying. Always treat
    /// the webhook as the authoritative source and use this endpoint only as a
    /// fallback / UX improvement.
    /// </summary>
    /// <remarks>
    /// Example redirect URLs the gateway generates:
    ///
    ///     GET /api/verify?reference=TXN_paystack_xxx                  (Paystack)
    ///     GET /api/verify?tx_ref=TXN_flutterwave_xxx                  (Flutterwave)
    ///     GET /api/verify?paymentReference=MNFY|20240101|xxx          (Monnify)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HandleRedirect()
    {
        // ── Step 1: extract the transaction reference from any query-string key ─
        var qs = HttpContext.Request.Query;

        // Check all known reference key names in order of specificity.
        var reference = qs["reference"].FirstOrDefault()
                     ?? qs["tx_ref"].FirstOrDefault()
                     ?? qs["paymentReference"].FirstOrDefault()
                     ?? qs["session_id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(reference))
        {
            _log.LogWarning("Redirect arrived with no recognisable reference parameter. Query: {Query}",
                HttpContext.Request.QueryString);
            return BadRequest(ApiResponse<object>.Fail(
                "Could not find a transaction reference in the query string. " +
                "Expected: reference, tx_ref, paymentReference, or session_id.",
                "NO_REFERENCE"));
        }

        _log.LogInformation("Redirect verification for reference: {Reference}", reference);

        // ── Step 2: detect gateway from reference prefix ──────────────────────
        //
        // GatewayExtractor can infer the gateway from well-known reference
        // prefixes (e.g. "FLW_" → Flutterwave, "MNFY|" → Monnify).
        // When it can't, it returns Automatic and VerifyPaymentAsync tries all.
        var gatewayValues = new[] { reference };
        var gateway = GatewayExtractor.DetectGatewayFromWebhook(gatewayValues);

        _log.LogInformation("Inferred gateway: {Gateway}", gateway);

        // ── Step 3: verify with the SDK ───────────────────────────────────────
        try
        {
            var result = await _payment.VerifyPaymentAsync(reference, gateway);

            // ── Step 4: update the order if it exists ─────────────────────────
            var order = _orders.GetByTransactionRef(reference);

            if (result.Success && result.Status == PaymentStatus.Successful && order is not null)
            {
                _orders.MarkAsPaid(order.OrderId, reference);
                _log.LogInformation("Redirect: order {OrderId} confirmed paid", order.OrderId);
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                transactionReference = result.TransactionReference,
                status               = result.Status.ToString(),
                amount               = result.Amount,
                currency             = result.Currency,
                paymentDate          = result.PaymentDate,
                paymentMethod        = result.PaymentMethod,
                orderId              = order?.OrderId,
                message              = result.Message,
            }, result.Success ? "Payment verified" : "Verification returned non-success"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error verifying redirect for reference {Reference}", reference);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while verifying the payment.", "VERIFY_ERROR"));
        }
    }
}
