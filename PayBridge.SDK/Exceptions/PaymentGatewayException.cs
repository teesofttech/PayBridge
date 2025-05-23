namespace PayBridge.SDK.Exceptions;
internal class PaymentGatewayException : PayBridgeException
{
    /// <summary>
    /// Gets the payment gateway where the error occurred
    /// </summary>
    public string Gateway { get; }

    /// <summary>
    /// Creates a new instance of the PaymentGatewayException class
    /// </summary>
    public PaymentGatewayException() : base() { }

    /// <summary>
    /// Creates a new instance of the PaymentGatewayException class with a specified error message
    /// </summary>
    public PaymentGatewayException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the PaymentGatewayException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception
    /// </summary>
    public PaymentGatewayException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Creates a new instance of the PaymentGatewayException class with a specified error message,
    /// gateway name, and a reference to the inner exception that is the cause of this exception
    /// </summary>
    public PaymentGatewayException(string message, string gateway, Exception innerException)
        : base(message, innerException)
    {
        Gateway = gateway;
    }
}
