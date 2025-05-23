using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;

namespace PayBridge.SDK;
public class TransactionRepository(PayBridgeDbContext dbContext, ILogger<TransactionRepository> logger) : ITransactionRepository
{
    private readonly PayBridgeDbContext _dbContext = dbContext;
    private readonly ILogger<TransactionRepository> _logger = logger;

    /// <inheritdoc/>
    public async Task<PaymentTransaction> CreateAsync(PaymentTransaction transaction)
    {
        if (transaction == null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        // Generate a new ID if not provided
        if (string.IsNullOrEmpty(transaction.Id))
        {
            transaction.Id = Guid.NewGuid().ToString();
        }

        // Set creation timestamp if not provided
        if (transaction.CreatedAt == default)
        {
            transaction.CreatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("Creating transaction with reference: {Reference}", transaction.TransactionReference);

        await _dbContext.Transactions.AddAsync(transaction);
        await _dbContext.SaveChangesAsync();

        return transaction;
    }

    /// <inheritdoc/>
    public async Task<PaymentTransaction> GetByReferenceAsync(string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            throw new ArgumentException("Transaction reference cannot be null or empty", nameof(reference));
        }

        _logger.LogInformation("Getting transaction by reference: {Reference}", reference);

        return await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.TransactionReference == reference);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentTransaction>> GetByCustomerEmailAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentException("Customer email cannot be null or empty", nameof(email));
        }

        _logger.LogInformation("Getting transactions for customer: {Email}", email);

        return await _dbContext.Transactions
            .Where(t => t.CustomerEmail == email)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentTransaction>> GetByStatusAsync(PaymentStatus status)
    {
        _logger.LogInformation("Getting transactions with status: {Status}", status);

        return await _dbContext.Transactions
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction)
    {
        if (transaction == null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        _logger.LogInformation("Updating transaction with reference: {Reference}", transaction.TransactionReference);

        _dbContext.Transactions.Update(transaction);
        await _dbContext.SaveChangesAsync();

        return transaction;
    }
}
