using PayBridge.SDK.Domain.Entities;
using PayBridge.SDK.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Infrastructure.Repositories;
internal class TransactionRepository : ITransactionRepository
{
    private readonly PayBridgeDbContext _context;

    public TransactionRepository(PayBridgeDbContext context)
    {
        _context = context;
    }

    public async Task LogTransaction(TransactionRecord transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
    }
}
