using PayBridge.SDK.Domain.Entities;

namespace PayBridge.SDK.Application.Interfaces;
internal interface ITransactionRepository
{
    Task SaveTransactionAsync(TransactionRecord transaction);
    Task<TransactionRecord?> GetTransactionByIdAsync(string transactionId);
    Task<IEnumerable<TransactionRecord>> GetAllTransactionsAsync();
}
