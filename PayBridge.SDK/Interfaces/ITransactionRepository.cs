using PayBridge.SDK.Entities;

namespace PayBridge.SDK;

public interface ITransactionRepository
{
    Task<PaymentTransaction> CreateAsync(PaymentTransaction transaction);
    Task<PaymentTransaction> GetByReferenceAsync(string reference);
    Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction);
    Task<IEnumerable<PaymentTransaction>> GetByCustomerEmailAsync(string email);
}
