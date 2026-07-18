using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK;

public sealed class RefundRepository(
    PayBridgeDbContext dbContext,
    ILogger<RefundRepository> logger) : IRefundRepository
{
    private static readonly SemaphoreSlim ReservationLock = new(1, 1);
    private readonly PayBridgeDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly ILogger<RefundRepository> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<RefundTransaction> CreateAsync(RefundTransaction refund)
    {
        ArgumentNullException.ThrowIfNull(refund);
        PrepareForInsert(refund);
        await _dbContext.Refunds.AddAsync(refund);
        await _dbContext.SaveChangesAsync();
        return refund;
    }

    public async Task<bool> TryReserveAsync(
        RefundTransaction refund,
        decimal capturedAmount)
    {
        ArgumentNullException.ThrowIfNull(refund);
        if (capturedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedAmount));
        }

        await ReservationLock.WaitAsync();
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
            var reservedAmounts = await _dbContext.Refunds
                .Where(item =>
                    item.PaymentTransactionReference == refund.PaymentTransactionReference &&
                    (item.Status == PaymentStatus.Pending ||
                     item.Status == PaymentStatus.Refunded))
                .Select(item => item.Amount)
                .ToListAsync();
            var reservedAmount = reservedAmounts.Sum();

            if (reservedAmount + refund.Amount > capturedAmount)
            {
                await transaction.RollbackAsync();
                return false;
            }

            refund.Status = PaymentStatus.Pending;
            PrepareForInsert(refund);
            await _dbContext.Refunds.AddAsync(refund);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        finally
        {
            ReservationLock.Release();
        }
    }

    public async Task<RefundTransaction?> GetByReferenceAsync(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return await _dbContext.Refunds
            .FirstOrDefaultAsync(refund => refund.RefundReference == reference);
    }

    public async Task<IEnumerable<RefundTransaction>> GetByPaymentReferenceAsync(
        string paymentReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        return await _dbContext.Refunds
            .Where(refund => refund.PaymentTransactionReference == paymentReference)
            .OrderByDescending(refund => refund.CreatedAt)
            .ToListAsync();
    }

    public async Task<RefundTransaction> UpdateAsync(RefundTransaction refund)
    {
        ArgumentNullException.ThrowIfNull(refund);
        _dbContext.Refunds.Update(refund);
        await _dbContext.SaveChangesAsync();
        return refund;
    }

    private static void PrepareForInsert(RefundTransaction refund)
    {
        if (string.IsNullOrWhiteSpace(refund.Id))
        {
            refund.Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(refund.RefundReference))
        {
            refund.RefundReference = refund.Id;
        }

        if (refund.CreatedAt == default)
        {
            refund.CreatedAt = DateTime.UtcNow;
        }
    }
}
