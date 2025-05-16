using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Exceptions;
internal class PaymentMethodNotFoundException : PayBridgeException
{
    /// <summary>
    /// Gets the payment method ID that was not found
    /// </summary>
    public string PaymentMethodId { get; }

    /// <summary>
    /// Creates a new instance of the PaymentMethodNotFoundException class
    /// </summary>
    public PaymentMethodNotFoundException() : base() { }

    /// <summary>
    /// Creates a new instance of the PaymentMethodNotFoundException class with a specified error message
    /// </summary>
    public PaymentMethodNotFoundException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the PaymentMethodNotFoundException class with a specified error message
    /// and payment method ID
    /// </summary>
    public PaymentMethodNotFoundException(string message, string paymentMethodId) : base(message)
    {
        PaymentMethodId = paymentMethodId;
    }
}
