using PayBridge.SDK.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Domain.Entities;
public class PaymentMethod
{
    /// <summary>
    /// Unique identifier for the payment method
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Reference provided by the payment gateway
    /// </summary>
    public string PaymentMethodReference { get; set; }

    /// <summary>
    /// Customer email associated with this payment method
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Customer name associated with this payment method
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Type of payment method
    /// </summary>
    public PaymentMethodType Type { get; set; }

    /// <summary>
    /// Last 4 digits (for cards)
    /// </summary>
    public string Last4 { get; set; }

    /// <summary>
    /// Expiry month (for cards)
    /// </summary>
    public string ExpiryMonth { get; set; }

    /// <summary>
    /// Expiry year (for cards)
    /// </summary>
    public string ExpiryYear { get; set; }

    /// <summary>
    /// Card brand or bank name
    /// </summary>
    public string Brand { get; set; }

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
