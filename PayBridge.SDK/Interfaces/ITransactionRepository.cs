using PayBridge.SDK.Entities;

namespace PayBridge.SDK;

public interface ITransactionRepository
{
    /// <summary>
    ///  Creates a new payment transaction in the repository
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    Task<PaymentTransaction> CreateAsync(PaymentTransaction transaction);

    /// <summary>
    ///  Gets a payment transaction by its reference
    /// </summary>
    /// <param name="reference"></param>
    /// <returns></returns>
    Task<PaymentTransaction> GetByReferenceAsync(string reference);

    /// <summary>
    /// updates an existing payment transaction
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction);

    /// <summary>
    /// Gets payment transactions for a specific customer by their email address
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    Task<IEnumerable<PaymentTransaction>> GetByCustomerEmailAsync(string email);
}
