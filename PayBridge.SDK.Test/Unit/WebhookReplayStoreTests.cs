using FluentAssertions;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Services;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class WebhookReplayStoreTests
{
    [Fact]
    public void Concurrent_identical_deliveries_allow_only_one_processor()
    {
        var store = new WebhookReplayStore();
        var payload = "signed-provider-payload"u8.ToArray();

        var accepted = Enumerable.Range(0, 16)
            .AsParallel()
            .Select(index => store.TryStart(
                PaymentGatewayType.Paystack,
                payload,
                out var receiptId))
            .ToArray();

        accepted.Count(value => value).Should().Be(1);
    }

    [Fact]
    public void Abandoned_delivery_can_be_retried()
    {
        var store = new WebhookReplayStore();
        var payload = "signed-provider-payload"u8.ToArray();
        store.TryStart(PaymentGatewayType.Paystack, payload, out var receiptId)
            .Should().BeTrue();

        store.Abandon(receiptId);

        store.TryStart(PaymentGatewayType.Paystack, payload, out _).Should().BeTrue();
    }

    [Fact]
    public void Repeated_fulfillment_does_not_change_the_paid_timestamp()
    {
        var orders = new OrderService();
        var order = orders.Create("customer@example.test", 100m, "NGN", "Order");
        orders.MarkAsPaid(order.OrderId, "PAY-1").Should().BeTrue();
        var paidAt = order.PaidAt;

        orders.MarkAsPaid(order.OrderId, "PAY-1").Should().BeTrue();

        order.PaidAt.Should().Be(paidAt);
    }
}
