namespace PayBridge.SDK.Exceptions;
internal class PaymentValidationException : PayBridgeException
{
    /// <summary>
    /// Gets the validation error details
    /// </summary>
    public string[] ValidationErrors { get; }

    /// <summary>
    /// Creates a new instance of the PaymentValidationException class
    /// </summary>
    public PaymentValidationException() : base() { }

    /// <summary>
    /// Creates a new instance of the PaymentValidationException class with a specified error message
    /// </summary>
    public PaymentValidationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the PaymentValidationException class with a specified error message
    /// and validation errors
    /// </summary>
    public PaymentValidationException(string message, string[] validationErrors) : base(message)
    {
        ValidationErrors = validationErrors;
    }
}
