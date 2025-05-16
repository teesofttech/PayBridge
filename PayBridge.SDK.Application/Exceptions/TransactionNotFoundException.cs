using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Exceptions;
internal class TransactionNotFoundException : PayBridgeException
{
    /// <summary>
    /// Gets the transaction reference that was not found
    /// </summary>
    public string TransactionReference { get; }

    /// <summary>
    /// Creates a new instance of the TransactionNotFoundException class
    /// </summary>
    public TransactionNotFoundException() : base() { }

    /// <summary>
    /// Creates a new instance of the TransactionNotFoundException class with a specified error message
    /// </summary>
    public TransactionNotFoundException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the TransactionNotFoundException class with a specified error message
    /// and transaction reference
    /// </summary>
    public TransactionNotFoundException(string message, string transactionReference) : base(message)
    {
        TransactionReference = transactionReference;
    }
}
