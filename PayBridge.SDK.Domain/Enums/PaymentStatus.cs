using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Domain.Enums;
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been initiated but not completed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment has been successfully completed
    /// </summary>
    Successful = 1,

    /// <summary>
    /// Payment has failed
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Payment was cancelled by the customer
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Payment has been refunded
    /// </summary>
    Refunded = 4
}