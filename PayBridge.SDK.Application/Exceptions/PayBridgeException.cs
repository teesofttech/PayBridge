using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Exceptions;
internal class PayBridgeException : Exception
{
    /// <summary>
    /// Creates a new instance of the PayBridgeException class
    /// </summary>
    public PayBridgeException() : base() { }

    /// <summary>
    /// Creates a new instance of the PayBridgeException class with a specified error message
    /// </summary>
    public PayBridgeException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance of the PayBridgeException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception
    /// </summary>
    public PayBridgeException(string message, Exception innerException) : base(message, innerException) { }
   

}
