using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Json;
using PayBridge.SDK.Dtos.Webhooks;
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
/// 2. How to verify the provider signature before parsing the body.
/// 3. How to extract the transaction reference after authentication.
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
/// e.g. "https://abcd1234.ngrok.io/api/webhook/Paystack".
/// </summary>
[ApiController]
[Route("api/webhook")]
[Produces("application/json")]
public class WebhookController : ControllerBase
{
    private readonly IPaymentService _payment;
    private readonly IWebhookSignatureVerifier _webhookVerifier;
    private readonly OrderService _orders;
    private readonly ILogger<WebhookController> _log;

    public WebhookController(
        IPaymentService paymentService,
        IWebhookSignatureVerifier webhookVerifier,
        OrderService orderService,
        ILogger<WebhookController> logger)
    {
        _payment = paymentService;
        _webhookVerifier = webhookVerifier;
        _orders = orderService;
        _log = logger;
    }

    // ── POST /api/webhook/{gateway} ──────────────────────────────────────────

    /// <summary>
    /// Receives a webhook notification from any supported payment gateway.
    ///
    /// The gateway is explicit in the route so its signature can be verified
    /// before the JSON body is parsed.
    ///
    /// **Important:** Reject unauthenticated deliveries, then acknowledge valid
    /// deliveries quickly. Heavy processing should be offloaded to a background
    /// queue in production because gateways retry slow or failed deliveries.
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
    [HttpPost("{gateway}")]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(
        PaymentGatewayType gateway,
        CancellationToken cancellationToken)
    {
        // Preserve the exact bytes. Parsing or reserializing before verification
        // changes the signed content and invalidates raw-body signatures.
        using var bodyBuffer = new MemoryStream();
        await Request.Body.CopyToAsync(bodyBuffer, cancellationToken);
        var rawBody = bodyBuffer.ToArray();

        var headers = Request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var verification = _webhookVerifier.Verify(new WebhookVerificationRequest(
            gateway,
            rawBody,
            headers,
            Request.Method,
            Request.GetDisplayUrl()));

        if (!verification.IsValid)
        {
            _log.LogWarning(
                "Rejected unauthenticated {Gateway} webhook: {Reason}",
                gateway,
                verification.FailureReason);
            return Unauthorized(new { received = false, reason = "invalid_signature" });
        }

        string? reference;
        if (gateway == PaymentGatewayType.PeachPayments &&
            Request.ContentType?.StartsWith(
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            var fields = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(rawBody))
                .ToDictionary(
                    field => field.Key,
                    field => field.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
            reference = GatewayExtractor.ExtractReferenceFromWebhook(fields, gateway);
        }
        else
        {
            JsonDocument webhookBody;
            try
            {
                webhookBody = JsonDocument.Parse(rawBody);
            }
            catch (JsonException)
            {
                _log.LogWarning("Rejected malformed {Gateway} webhook JSON", gateway);
                return BadRequest(new { received = false, reason = "invalid_json" });
            }

            using (webhookBody)
            {
                reference = GatewayExtractor.ExtractReferenceFromWebhook(
                webhookBody.RootElement,
                gateway);
            }
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            _log.LogWarning(
                "Authenticated {Gateway} webhook did not contain a transaction reference",
                gateway);
            return BadRequest(new { received = true, processed = false, reason = "no_reference" });
        }

        _log.LogInformation(
            "Authenticated {Gateway} webhook for {Reference}",
            gateway,
            reference);

        // ── Step 3: verify the payment with the gateway ───────────────────────
        //
        // NEVER trust the webhook body alone — always call VerifyPaymentAsync to
        // confirm the transaction status server-side before fulfilling the order.
        var paymentVerification = await _payment.VerifyPaymentAsync(reference, gateway);

        if (!paymentVerification.Success ||
            paymentVerification.Status != PaymentStatus.Successful)
        {
            _log.LogWarning(
                "Webhook for {Reference} — verification failed or status={Status}",
                reference,
                paymentVerification.Status);

            return Ok(new
            {
                received = true,
                processed = false,
                status = paymentVerification.Status.ToString()
            });
        }

        // ── Step 4: fulfil the order ──────────────────────────────────────────
        //
        // Look up the order using the orderId we embedded in Metadata, then mark
        // it as paid. In a real app you'd:
        //   • Send a confirmation email
        //   • Provision the purchased product / subscription
        //   • Emit a domain event for downstream services
        var orderId = paymentVerification.Metadata.GetValueOrDefault("orderId")
                   ?? _orders.GetByTransactionRef(reference)?.OrderId;
        var order = orderId is null ? null : _orders.GetById(orderId);

        if (order is not null &&
            paymentVerification.TransactionReference.Equals(
                reference,
                StringComparison.Ordinal) &&
            paymentVerification.Amount == order.Amount &&
            paymentVerification.Currency.Equals(
                order.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            _orders.MarkAsPaid(order.OrderId, reference);
            _log.LogInformation("Order {OrderId} marked as paid ✓", order.OrderId);
        }
        else
        {
            _log.LogWarning(
                "Verified payment {Reference} did not match the original order",
                reference);
            return Ok(new
            {
                received = true,
                processed = false,
                status = "order_mismatch"
            });
        }

        return Ok(new
        {
            received = true,
            processed = true,
            reference,
            gateway = gateway.ToString()
        });
    }
}
