using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class PaymentIdempotencyPersistenceTests
{
    [Fact]
    public async Task Database_rejects_duplicate_payment_idempotency_keys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PayBridgeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new PayBridgeDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Transactions.Add(NewTransaction("first", "checkout-123"));
        await context.SaveChangesAsync();
        context.Transactions.Add(NewTransaction("second", "checkout-123"));

        var action = () => context.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void Model_defines_bounded_unique_idempotency_key()
    {
        var options = new DbContextOptionsBuilder<PayBridgeDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new PayBridgeDbContext(options);
        var entity = context.Model.FindEntityType(typeof(PaymentTransaction))!;
        var property = entity.FindProperty(nameof(PaymentTransaction.IdempotencyKey))!;
        var index = entity.GetIndexes().Single(item =>
            item.Properties.Count == 1 && item.Properties[0] == property);

        property.GetMaxLength().Should().Be(255);
        index.IsUnique.Should().BeTrue();
    }

    private static PaymentTransaction NewTransaction(string id, string key) => new()
    {
        Id = id,
        IdempotencyKey = key,
        RequestFingerprint = new string('A', 64),
        TransactionReference = $"PAY-{id}",
        Amount = 100m,
        Currency = "NGN",
        CustomerEmail = "customer@example.test",
        Status = PaymentStatus.Pending,
        Gateway = PaymentGatewayType.Paystack,
        CreatedAt = DateTime.UtcNow
    };
}
