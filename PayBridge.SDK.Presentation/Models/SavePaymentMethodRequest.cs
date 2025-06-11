using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Presentation.Models;

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
