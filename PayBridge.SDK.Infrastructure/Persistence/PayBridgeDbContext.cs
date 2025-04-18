﻿using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Domain.Entities;

namespace PayBridge.SDK.Infrastructure.Persistence;

public class PayBridgeDbContext : DbContext
{
    public PayBridgeDbContext(DbContextOptions<PayBridgeDbContext> options)
        : base(options) { }

    public DbSet<TransactionRecord> Transactions { get; set; }
}
