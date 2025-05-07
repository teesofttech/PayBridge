using PayBridge.SDK.Domain.Entities;

namespace PayBridge.SDK.Domain;

public interface ITransactionRepository
{
    Task<PaymentTransaction> CreateAsync(PaymentTransaction transaction);
    Task<PaymentTransaction> GetByReferenceAsync(string reference);
    Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction);
    Task<IEnumerable<PaymentTransaction>> GetByCustomerEmailAsync(string email);
}
