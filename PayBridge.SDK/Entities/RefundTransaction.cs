using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Entities;
public class RefundTransaction
{
    /// <summary>
    /// Optional application-level idempotency key for the refund attempt
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Unique identifier for the refund
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Reference provided by the payment gateway
    /// </summary>
    public string RefundReference { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the original payment transaction
    /// </summary>
    public string PaymentTransactionReference { get; set; } = string.Empty;

    /// <summary>
    /// Amount refunded
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the refund
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the refund
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Payment gateway used for this refund
    /// </summary>
    public PaymentGatewayType Gateway { get; set; }

    /// <summary>
    /// Raw response from the payment gateway
    /// </summary>
    public string GatewayResponse { get; set; } = string.Empty;

    /// <summary>
    /// Stable fingerprint of the refund request used to validate idempotent retries
    /// </summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// When the refund was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the refund was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}
