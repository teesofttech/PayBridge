using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Domain;
using PayBridge.SDK.Domain.Entities;
using PayBridge.SDK.Infrastructure.Persistence;

namespace PayBridge.SDK.Infrastructure.Repositories;
public class TransactionRepository : ITransactionRepository
{
    private readonly PayBridgeDbContext _context;

    public TransactionRepository(PayBridgeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TransactionRecord>> GetAllTransactionsAsync()
    {
        return await _context.Transactions
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<TransactionRecord?> GetTransactionByIdAsync(string transactionId)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionUniqueId == transactionId);
    }


    public async Task SaveTransactionAsync(TransactionRecord transaction)
    {
        await _context.Transactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }
}
