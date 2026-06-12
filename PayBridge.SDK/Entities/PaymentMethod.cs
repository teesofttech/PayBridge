using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Entities;
public class PaymentMethod
{
    /// <summary>
    /// Unique identifier for the payment method
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Reference provided by the payment gateway
    /// </summary>
    public string PaymentMethodReference { get; set; } = string.Empty;

    /// <summary>
    /// Customer email associated with this payment method
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Customer name associated with this payment method
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Type of payment method
    /// </summary>
    public PaymentMethodType Type { get; set; }

    /// <summary>
    /// Last 4 digits (for cards)
    /// </summary>
    public string Last4 { get; set; } = string.Empty;

    /// <summary>
    /// Expiry month (for cards)
    /// </summary>
    public string ExpiryMonth { get; set; } = string.Empty;

    /// <summary>
    /// Expiry year (for cards)
    /// </summary>
    public string ExpiryYear { get; set; } = string.Empty;

    /// <summary>
    /// Card brand or bank name
    /// </summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// Payment gateway used for this payment method
    /// </summary>
    public PaymentGatewayType Gateway { get; set; }

    /// <summary>
    /// Whether this is the customer's default payment method
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// When the payment method was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the payment method was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
