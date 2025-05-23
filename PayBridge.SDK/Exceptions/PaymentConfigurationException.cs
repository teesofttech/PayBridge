namespace PayBridge.SDK.Exceptions;
internal class PaymentConfigurationException : PayBridgeException
{
    /// <summary>
    /// Creates a new instance of the PaymentConfigurationException class
    /// </summary>
    public PaymentConfigurationException() : base() { }

    /// <summary>
    /// Creates a new instance of the PaymentConfigurationException class with a specified error message
    /// </summary>
    public PaymentConfigurationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the PaymentConfigurationException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception
    /// </summary>
    public PaymentConfigurationException(string message, Exception innerException) : base(message, innerException) { }

}
