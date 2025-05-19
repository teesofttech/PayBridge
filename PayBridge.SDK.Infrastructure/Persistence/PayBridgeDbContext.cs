using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Domain.Entities;

namespace PayBridge.SDK.Infrastructure.Persistence;

public class PayBridgeDbContext : DbContext
{
    public PayBridgeDbContext(DbContextOptions<PayBridgeDbContext> options)
        : base(options) { }

    public DbSet<PaymentTransaction> Transactions { get; set; }
    public DbSet<RefundTransaction> Refunds { get; set; }   
}
