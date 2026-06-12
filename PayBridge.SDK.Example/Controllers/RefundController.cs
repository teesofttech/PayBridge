using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// SCENARIO 6 — Initiating full and partial refunds.
///
/// Not every gateway supports refunds via API — check the gateway's documentation
/// and your merchant agreement. The SDK surfaces a consistent
/// <c>IPaymentService.RefundPaymentAsync</c> method; if the gateway does not
/// support it, a <see cref="PayBridge.SDK.Exceptions.PaymentGatewayException"/>
/// is thrown with a clear message.
///
/// Gateways with refund support (as of SDK v1.2):
///   Paystack ✓ | Flutterwave ✓ | Stripe ✓ | Korapay ✓ | PeachPayments ✓
///
/// Partial refunds: pass an <c>amount</c> less than the original charge.
/// Not all gateways support partial refunds — Paystack and Stripe do; check
/// others in their documentation.
/// </summary>
[ApiController]
[Route("api/refund")]
[Produces("application/json")]
public class RefundController : ControllerBase
{
    private readonly IPaymentService _payment;
    private readonly OrderService    _orders;
    private readonly ILogger<RefundController> _log;

    public RefundController(
        IPaymentService paymentService,
        OrderService    orderService,
        ILogger<RefundController> logger)
    {
        _payment = paymentService;
        _orders  = orderService;
        _log     = logger;
    }

    // ── POST /api/refund ─────────────────────────────────────────────────────

    /// <summary>
    /// Initiates a full or partial refund for a successful transaction.
    ///
    /// **Full refund:** set <c>amount</c> equal to the original charge amount.
    ///
    /// **Partial refund:** set <c>amount</c> to a smaller value.
    /// Not all gateways support partial refunds — the SDK will throw a
    /// <c>PaymentGatewayException</c> if the gateway rejects the partial amount.
    ///
    /// The SDK auto-detects the gateway from the <c>transactionReference</c>
    /// prefix (e.g. "FLW_" → Flutterwave). If it cannot detect it, Automatic
    /// mode tries each active gateway in turn.
    /// </summary>
    /// <remarks>
    /// Sample request — full refund of a Paystack transaction:
    ///
    ///     POST /api/refund
    ///     {
    ///         "transactionReference": "TXN_paystack_abc123",
    ///         "amount": 5000,
    ///         "reason": "Customer requested cancellation within 24 h"
    ///     }
    ///
    /// Sample request — partial refund (₦2,500 of a ₦5,000 charge):
    ///
    ///     POST /api/refund
    ///     {
    ///         "transactionReference": "TXN_paystack_abc123",
    ///         "amount": 2500,
    ///         "reason": "Partial service not delivered"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Refund([FromBody] RefundModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TransactionReference))
            return BadRequest(ApiResponse<object>.Fail("transactionReference is required.", "INVALID_REQUEST"));

        if (model.Amount <= 0)
            return BadRequest(ApiResponse<object>.Fail("amount must be greater than zero.", "INVALID_AMOUNT"));

        _log.LogInformation(
            "Refund requested — ref: {Reference}, amount: {Amount}, reason: {Reason}",
            model.TransactionReference, model.Amount, model.Reason);

        try
        {
            // ── Build the SDK refund request ──────────────────────────────────
            var refundRequest = new RefundRequest
            {
                TransactionReference = model.TransactionReference,
                Amount               = model.Amount,
                Reason               = model.Reason,
            };

            var result = await _payment.RefundPaymentAsync(refundRequest);

            if (!result.Success)
            {
                _log.LogWarning("Refund failed for {Reference}: {Message}",
                    model.TransactionReference, result.Message);
                return BadRequest(ApiResponse<object>.Fail(result.Message, "REFUND_FAILED"));
            }

            // ── Update the order status ───────────────────────────────────────
            //
            // Only mark as fully refunded if the refunded amount equals the
            // original. For partial refunds you may want a different status or
            // to track remaining balance — adapt to your business rules.
            var order = _orders.GetByTransactionRef(model.TransactionReference);
            if (order is not null)
            {
                var isFullRefund = result.Amount >= order.Amount;
                if (isFullRefund)
                    _orders.MarkAsRefunded(order.OrderId);

                _log.LogInformation(
                    "Order {OrderId} — {RefundType} refund of {Amount} processed",
                    order.OrderId, isFullRefund ? "full" : "partial", result.Amount);
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                refundReference      = result.RefundReference,
                transactionReference = result.TransactionReference,
                amount               = result.Amount,
                status               = result.Status.ToString(),
                refundDate           = result.RefundDate,
                message              = result.Message,
            }, "Refund processed successfully"));
        }
        catch (InvalidOperationException opEx)
        {
            // Thrown when the gateway explicitly rejects the refund request,
            // e.g. "partial refund not supported" or "already refunded".
            _log.LogWarning(opEx, "Gateway rejected refund for {Reference}", model.TransactionReference);
            return BadRequest(ApiResponse<object>.Fail(opEx.Message, "GATEWAY_REJECTED"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error processing refund for {Reference}", model.TransactionReference);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An unexpected error occurred. Check the server logs.", "INTERNAL_ERROR"));
        }
    }
}
