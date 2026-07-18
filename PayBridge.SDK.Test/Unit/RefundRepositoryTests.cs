using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class RefundRepositoryTests
{
    [Fact]
    public async Task DbContext_model_defines_refund_reservation_index()
    {
        await using var database = await RefundDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var entity = context.Model.FindEntityType(typeof(RefundTransaction));
        var index = entity!.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(RefundTransaction.PaymentTransactionReference),
                 nameof(RefundTransaction.Status)]));

        index.GetDatabaseName().Should()
            .Be("IX_Refunds_PaymentTransactionReference_Status");
    }

    [Fact]
    public async Task TryReserveAsync_rejects_cumulative_pending_and_refunded_amounts()
    {
        await using var database = await RefundDatabase.CreateAsync();
        await database.SeedPaymentAsync(100m);

        await using (var context = database.CreateContext())
        {
            var repository = CreateRepository(context);
            (await repository.TryReserveAsync(NewRefund("first", 60m), 100m)).Should().BeTrue();
            var completed = await repository.GetByReferenceAsync("first");
            completed!.Status = PaymentStatus.Refunded;
            await repository.UpdateAsync(completed);
        }

        await using (var context = database.CreateContext())
        {
            var repository = CreateRepository(context);
            (await repository.TryReserveAsync(NewRefund("second", 40m), 100m)).Should().BeTrue();
            (await repository.TryReserveAsync(NewRefund("third", 0.01m), 100m)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task TryReserveAsync_does_not_count_failed_attempts()
    {
        await using var database = await RefundDatabase.CreateAsync();
        await database.SeedPaymentAsync(100m);

        await using var context = database.CreateContext();
        var repository = CreateRepository(context);
        var failed = NewRefund("failed", 100m);
        failed.Status = PaymentStatus.Failed;
        await repository.CreateAsync(failed);

        (await repository.TryReserveAsync(NewRefund("replacement", 100m), 100m))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_reservations_cannot_exceed_captured_amount()
    {
        await using var database = await RefundDatabase.CreateAsync();
        await database.SeedPaymentAsync(100m);

        var attempts = Enumerable.Range(0, 8).Select(async index =>
        {
            await using var context = database.CreateContext();
            var repository = CreateRepository(context);
            return await repository.TryReserveAsync(NewRefund($"refund-{index}", 60m), 100m);
        });

        var results = await Task.WhenAll(attempts);

        results.Count(result => result).Should().Be(1);
        await using var assertionContext = database.CreateContext();
        var reservedAmounts = await assertionContext.Refunds
            .Where(refund => refund.Status == PaymentStatus.Pending ||
                refund.Status == PaymentStatus.Refunded)
            .Select(refund => refund.Amount)
            .ToListAsync();
        var reserved = reservedAmounts.Sum();
        reserved.Should().BeLessThanOrEqualTo(100m);
    }

    private static RefundRepository CreateRepository(PayBridgeDbContext context) =>
        new(context, NullLogger<RefundRepository>.Instance);

    private static RefundTransaction NewRefund(string id, decimal amount) => new()
    {
        Id = id,
        RefundReference = id,
        PaymentTransactionReference = "PAYMENT-1",
        Amount = amount,
        Currency = "NGN",
        Reason = "requested_by_customer",
        Gateway = PaymentGatewayType.Flutterwave,
        Status = PaymentStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class RefundDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly DbContextOptions<PayBridgeDbContext> _options;

        private RefundDatabase(
            SqliteConnection keepAlive,
            DbContextOptions<PayBridgeDbContext> options)
        {
            _keepAlive = keepAlive;
            _options = options;
        }

        public static async Task<RefundDatabase> CreateAsync()
        {
            var name = $"refund-{Guid.NewGuid():N}";
            var connectionString = $"Data Source={name};Mode=Memory;Cache=Shared;Default Timeout=5";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<PayBridgeDbContext>()
                .UseSqlite(connectionString)
                .Options;
            await using var context = new PayBridgeDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new RefundDatabase(keepAlive, options);
        }

        public PayBridgeDbContext CreateContext() => new(_options);

        public async Task SeedPaymentAsync(decimal amount)
        {
            await using var context = CreateContext();
            context.Transactions.Add(new PaymentTransaction
            {
                Id = "payment-id",
                TransactionReference = "PAYMENT-1",
                Amount = amount,
                Currency = "NGN",
                Status = PaymentStatus.Successful,
                Gateway = PaymentGatewayType.Flutterwave,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();
    }
}
