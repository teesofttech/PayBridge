using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Presentation.Models;

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