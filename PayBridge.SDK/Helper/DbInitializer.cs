namespace PayBridge.SDK;

public static class DbInitializer
{
    public static void Initialize(PayBridgeDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Transactions.Any()) return;

        //var transactions = new List<TransactionRecord>
        //{
        //    new TransactionRecord { TransactionUniqueId = "TXN001", Gateway = "Flutterwave", Amount = 100, Currency = "USD", Status = "Success", CreatedAt = DateTime.UtcNow },
        //    new TransactionRecord { TransactionUniqueId = "TXN002", Gateway = "Paystack", Amount = 200, Currency = "NGN", Status = "Pending", CreatedAt = DateTime.UtcNow }
        //};

        // context.Transactions.AddRange(transactions);
        context.SaveChanges();
    }
}
