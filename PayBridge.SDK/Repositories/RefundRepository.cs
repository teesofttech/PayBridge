using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK;

public sealed class RefundRepository(
    PayBridgeDbContext dbContext,
    ILogger<RefundRepository> logger) : IRefundRepository
{
    private static readonly object ReservationLocksGate = new();
    private static readonly Dictionary<string, ReservationLock> ReservationLocks = [];
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
        if (refund.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException("amount", refund.Amount,
                "Refund amount must be positive.");
        }

        if (capturedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedAmount));
        }

        var reservationLock = AcquireReservationLock(refund.PaymentTransactionReference);
        await reservationLock.Semaphore.WaitAsync();
        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
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
            });
        }
        finally
        {
            reservationLock.Semaphore.Release();
            ReleaseReservationLock(refund.PaymentTransactionReference, reservationLock);
        }
    }

    public async Task<RefundTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return await _dbContext.Refunds
            .FirstOrDefaultAsync(refund => refund.IdempotencyKey == idempotencyKey);
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

    public async Task<RefundTransaction> FinalizeAsync(RefundTransaction refund, RefundResponse response)
    {
        ArgumentNullException.ThrowIfNull(refund);
        ArgumentNullException.ThrowIfNull(response);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            var trackedRefund = await _dbContext.Refunds
                .FirstOrDefaultAsync(item => item.Id == refund.Id);
            if (trackedRefund is null)
            {
                throw new InvalidOperationException($"Refund not found: {refund.Id}");
            }

            var payment = await _dbContext.Transactions
                .FirstOrDefaultAsync(item => item.TransactionReference == trackedRefund.PaymentTransactionReference);
            if (payment is null)
            {
                throw new InvalidOperationException(
                    $"Payment transaction not found: {trackedRefund.PaymentTransactionReference}");
            }

            trackedRefund.RefundReference = string.IsNullOrWhiteSpace(response.RefundReference)
                ? trackedRefund.Id
                : response.RefundReference;
            var previousStatus = trackedRefund.Status;
            trackedRefund.Status = response.Success ? response.Status : PaymentStatus.Failed;
            trackedRefund.ProcessedAt = trackedRefund.Status == PaymentStatus.Pending
                ? null
                : response.RefundDate == default ? DateTime.UtcNow : response.RefundDate;
            trackedRefund.GatewayResponse = JsonSerializer.Serialize(response);

            if (trackedRefund.Status == PaymentStatus.Refunded)
            {
                var confirmedAmount = await _dbContext.Refunds
                    .Where(item =>
                        item.PaymentTransactionReference == trackedRefund.PaymentTransactionReference &&
                        item.Status == PaymentStatus.Refunded)
                    .Select(item => item.Amount)
                    .SumAsync();

                if (previousStatus != PaymentStatus.Refunded)
                {
                    confirmedAmount += trackedRefund.Amount;
                }

                if (confirmedAmount >= payment.Amount)
                {
                    payment.Status = PaymentStatus.Refunded;
                    _dbContext.Transactions.Update(payment);
                }
            }

            _dbContext.Refunds.Update(trackedRefund);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return trackedRefund;
        });
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

        if (string.IsNullOrWhiteSpace(refund.RequestFingerprint))
        {
            refund.RequestFingerprint = string.Empty;
        }

        if (refund.CreatedAt == default)
        {
            refund.CreatedAt = DateTime.UtcNow;
        }
    }

    private static ReservationLock AcquireReservationLock(string paymentReference)
    {
        lock (ReservationLocksGate)
        {
            if (!ReservationLocks.TryGetValue(paymentReference, out var reservationLock))
            {
                reservationLock = new ReservationLock();
                ReservationLocks.Add(paymentReference, reservationLock);
            }

            reservationLock.Users++;
            return reservationLock;
        }
    }

    private static void ReleaseReservationLock(
        string paymentReference,
        ReservationLock reservationLock)
    {
        lock (ReservationLocksGate)
        {
            reservationLock.Users--;
            if (reservationLock.Users == 0)
            {
                ReservationLocks.Remove(paymentReference);
                reservationLock.Semaphore.Dispose();
            }
        }
    }

    private sealed class ReservationLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users { get; set; }
    }
}
