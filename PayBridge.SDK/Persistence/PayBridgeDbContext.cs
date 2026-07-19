using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Entities;

namespace PayBridge.SDK;

public class PayBridgeDbContext : DbContext
{
    public PayBridgeDbContext(DbContextOptions<PayBridgeDbContext> options)
        : base(options) { }

    public DbSet<PaymentTransaction> Transactions { get; set; }
    public DbSet<RefundTransaction> Refunds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(transaction => transaction.IdempotencyKey)
            .IsUnique();
        modelBuilder.Entity<PaymentTransaction>()
            .Property(transaction => transaction.IdempotencyKey)
            .HasMaxLength(255);
        modelBuilder.Entity<PaymentTransaction>()
            .Property(transaction => transaction.RequestFingerprint)
            .HasMaxLength(64);

        modelBuilder.Entity<RefundTransaction>()
            .HasIndex(refund => new { refund.PaymentTransactionReference, refund.Status });
        modelBuilder.Entity<RefundTransaction>()
            .HasIndex(refund => refund.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        modelBuilder.Entity<RefundTransaction>()
            .Property(refund => refund.IdempotencyKey)
            .HasMaxLength(255);
        modelBuilder.Entity<RefundTransaction>()
            .Property(refund => refund.RequestFingerprint)
            .HasMaxLength(64);
    }
}
