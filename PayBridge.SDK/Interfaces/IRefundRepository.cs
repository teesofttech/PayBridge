using PayBridge.SDK.Entities;
using PayBridge.SDK.Dtos.Response;

namespace PayBridge.SDK.Interfaces;

public interface IRefundRepository
{
    /// <summary>
    /// Creates a new refund in the repository
    /// </summary>
    Task<RefundTransaction> CreateAsync(RefundTransaction refund);

    /// <summary>
    /// Atomically reserves refundable balance by inserting a pending refund.
    /// </summary>
    Task<bool> TryReserveAsync(RefundTransaction refund, decimal capturedAmount);

    /// <summary>
    /// Gets a refund by its idempotency key.
    /// </summary>
    Task<RefundTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey);

    /// <summary>
    /// Gets a refund by its reference
    /// </summary>
    Task<RefundTransaction?> GetByReferenceAsync(string reference);

    /// <summary>
    /// Gets refunds for a specific payment transaction
    /// </summary>
    Task<IEnumerable<RefundTransaction>> GetByPaymentReferenceAsync(string paymentReference);

    /// <summary>
    /// Updates an existing refund
    /// </summary>
    Task<RefundTransaction> UpdateAsync(RefundTransaction refund);

    /// <summary>
    /// Finalizes a refund and recalculates the parent payment status transactionally.
    /// </summary>
    Task<RefundTransaction> FinalizeAsync(RefundTransaction refund, RefundResponse response);
}
