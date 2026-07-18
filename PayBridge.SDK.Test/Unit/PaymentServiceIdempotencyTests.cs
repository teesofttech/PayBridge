using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class PaymentServiceIdempotencyTests
{
    [Fact]
    public async Task Concurrent_requests_with_same_key_call_gateway_once_and_replay_result()
    {
        var fixture = CreateFixture();
        var request = NewRequest("checkout-123");

        var results = await Task.WhenAll(
            fixture.Service.CreatePaymentAsync(request, PaymentGatewayType.Paystack),
            fixture.Service.CreatePaymentAsync(request, PaymentGatewayType.Paystack));

        results.Should().OnlyContain(result => result.TransactionReference == "PAY-123");
        fixture.Gateway.Verify(gateway =>
            gateway.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
        fixture.Transactions.Verify(repository =>
            repository.CreateAsync(It.IsAny<PaymentTransaction>()), Times.Once);
    }

    [Fact]
    public async Task Reusing_key_with_different_parameters_is_rejected_without_provider_call()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreatePaymentAsync(
            NewRequest("checkout-123"), PaymentGatewayType.Paystack);
        var changed = NewRequest("checkout-123");
        changed.Amount = 200m;

        var action = () => fixture.Service.CreatePaymentAsync(
            changed, PaymentGatewayType.Paystack);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*different payment parameters*");
        fixture.Gateway.Verify(gateway =>
            gateway.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
    }

    private static Fixture CreateFixture()
    {
        PaymentTransaction? stored = null;
        var transactions = new Mock<ITransactionRepository>();
        transactions.Setup(repository => repository.GetByIdempotencyKeyAsync("checkout-123"))
            .ReturnsAsync(() => stored);
        transactions.Setup(repository => repository.CreateAsync(It.IsAny<PaymentTransaction>()))
            .Callback<PaymentTransaction>(transaction => stored = transaction)
            .ReturnsAsync((PaymentTransaction transaction) => transaction);
        transactions.Setup(repository => repository.UpdateAsync(It.IsAny<PaymentTransaction>()))
            .ReturnsAsync((PaymentTransaction transaction) => transaction);

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.GatewayType).Returns(PaymentGatewayType.Paystack);
        gateway.Setup(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResponse
            {
                Success = true,
                TransactionReference = "PAY-123",
                CheckoutUrl = "https://checkout.example/PAY-123",
                Status = PaymentStatus.Pending
            });

        var service = new PaymentService(
            transactions.Object,
            Mock.Of<IRefundRepository>(),
            [gateway.Object],
            NullLogger<PaymentService>.Instance,
            new PaymentGatewayConfig());
        return new Fixture(service, transactions, gateway);
    }

    private static PaymentRequest NewRequest(string key) => new()
    {
        IdempotencyKey = key,
        Amount = 100m,
        Currency = "NGN",
        CustomerEmail = "customer@example.test",
        CustomerName = "Customer"
    };

    private sealed record Fixture(
        PaymentService Service,
        Mock<ITransactionRepository> Transactions,
        Mock<IPaymentGateway> Gateway);
}
