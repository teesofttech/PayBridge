namespace PayBridge.SDK.Example.Models;

/// <summary>Request body for POST /api/refund.</summary>
public class RefundModel
{
    /// <summary>
    /// The transaction reference returned by the original payment response
    /// (<c>TransactionReference</c> field).
    /// </summary>
    /// <example>FLW_dc324e96d52b4bd48c401ff9194c15e8</example>
    public string TransactionReference { get; set; } = string.Empty;

    /// <summary>
    /// Amount to refund.
    /// <list type="bullet">
    ///   <item>Pass the full original amount for a <strong>full refund</strong>.</item>
    ///   <item>Pass a smaller value for a <strong>partial refund</strong> (gateway-dependent).</item>
    /// </list>
    /// </summary>
    /// <example>5000</example>
    public decimal Amount { get; set; }

    /// <summary>Reason shown to the customer / recorded in the gateway dashboard.</summary>
    /// <example>Customer requested cancellation within 24 h</example>
    public string Reason { get; set; } = string.Empty;
}
