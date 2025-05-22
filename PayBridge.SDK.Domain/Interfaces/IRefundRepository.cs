using PayBridge.SDK.Domain.Entities;

namespace PayBridge.SDK.Domain.Interfaces;
public interface IRefundRepository
{
    /// <summary>
    /// Creates a new refund in the repository
    /// </summary>
    Task<RefundTransaction> CreateAsync(RefundTransaction refund);

    /// <summary>
    /// Gets a refund by its reference
    /// </summary>
    Task<RefundTransaction> GetByReferenceAsync(string reference);

    /// <summary>
    /// Gets refunds for a specific payment transaction
    /// </summary>
    Task<IEnumerable<RefundTransaction>> GetByPaymentReferenceAsync(string paymentReference);

    /// <summary>
    /// Updates an existing refund
    /// </summary>
    Task<RefundTransaction> UpdateAsync(RefundTransaction refund);
}
