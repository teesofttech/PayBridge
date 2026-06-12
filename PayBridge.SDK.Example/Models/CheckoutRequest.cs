using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Example.Models;

/// <summary>
/// The request body sent to POST /api/payment.
/// Maps cleanly to <see cref="PayBridge.SDK.Dtos.Request.PaymentRequest"/> via
/// <see cref="PayBridge.SDK.Example.Mapper.CheckoutRequestMapper"/>.
/// </summary>
public class CheckoutRequest
{
    /// <summary>
    /// Amount to charge. Always in the smallest unit required by the gateway
    /// (e.g. 1000 = ₦1,000 for Paystack / Flutterwave / Korapay).
    /// Stripe requires the amount in the currency's smallest unit (cents for USD).
    /// </summary>
    /// <example>5000</example>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code, e.g. NGN, USD, ZAR, GHS, KES.
    /// When <see cref="Gateway"/> is <c>Automatic</c> the SDK uses the currency
    /// to select the most appropriate gateway.
    /// </summary>
    /// <example>NGN</example>
    public string Currency { get; set; } = "NGN";

    /// <summary>What the customer is paying for.</summary>
    /// <example>Order #1042 — 2× Pro Subscription</example>
    public string Description { get; set; } = string.Empty;

    /// <summary>Customer's e-mail address. Required by most gateways.</summary>
    /// <example>jane.doe@example.com</example>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>Customer's full name.</summary>
    /// <example>Jane Doe</example>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Customer's phone number (optional but recommended).</summary>
    /// <example>+2348012345678</example>
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// URL the gateway redirects the customer to after payment.
    /// Should point to your <c>GET /api/verify</c> endpoint so the result
    /// is automatically verified on arrival.
    /// </summary>
    /// <example>https://localhost:7252/api/verify</example>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL the gateway POSTs the server-to-server webhook notification to.
    /// Use ngrok / localtunnel when developing locally — see README.
    /// </summary>
    /// <example>https://your-ngrok-url.ngrok.io/api/webhook</example>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary key-value pairs forwarded to the gateway and echoed back
    /// in the webhook. Useful for tying the payment to an internal order ID.
    /// </summary>
    /// <example>{ "orderId": "ORD-1042", "plan": "pro" }</example>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Preferred payment method. Defaults to <c>Card</c>.
    /// Not all gateways honour this — consult the gateway's documentation.
    /// </summary>
    public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Card;

    /// <summary>
    /// Which gateway to use.
    /// <list type="bullet">
    ///   <item><c>Automatic (0)</c> — SDK picks the best gateway for the currency/region.</item>
    ///   <item><c>Paystack (2)</c> — Force Paystack regardless of currency.</item>
    ///   <item><c>Flutterwave (1)</c> — Force Flutterwave.</item>
    ///   <item><c>Stripe (3)</c> — Force Stripe.</item>
    ///   <item>… see <see cref="PaymentGatewayType"/> for all values.</item>
    /// </list>
    /// </summary>
    /// <example>2</example>
    public PaymentGatewayType? Gateway { get; set; }
}
