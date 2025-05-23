using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Entities;

namespace PayBridge.SDK;

public class PayBridgeDbContext : DbContext
{
    public PayBridgeDbContext(DbContextOptions<PayBridgeDbContext> options)
        : base(options) { }

    public DbSet<PaymentTransaction> Transactions { get; set; }
    public DbSet<RefundTransaction> Refunds { get; set; }
}
