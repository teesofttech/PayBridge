using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class PaymentServiceRefundTests
{
    [Fact]
    public async Task RefundPaymentAsync_persists_confirmed_full_refund_and_updates_payment()
    {
        var providerTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var fixture = CreateFixture(new RefundResponse
        {
            Success = true,
            RefundReference = "provider-refund",
            Amount = 100m,
            Status = PaymentStatus.Refunded,
            RefundDate = providerTimestamp
        });

        var response = await fixture.Service.RefundPaymentAsync(NewRequest(100m));

        response.Success.Should().BeTrue();
        fixture.Refund.Status.Should().Be(PaymentStatus.Refunded);
        fixture.Refund.RefundReference.Should().Be("provider-refund");
        fixture.Refund.ProcessedAt.Should().Be(providerTimestamp);
        fixture.Refunds.Verify(repository =>
            repository.UpdateAsync(fixture.Refund), Times.Once);
        fixture.Transaction.Status.Should().Be(PaymentStatus.Refunded);
        fixture.Transactions.Verify(repository =>
            repository.UpdateAsync(fixture.Transaction), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_keeps_payment_successful_while_provider_refund_is_pending()
    {
        var fixture = CreateFixture(new RefundResponse
        {
            Success = true,
            RefundReference = "pending-refund",
            Amount = 100m,
            Status = PaymentStatus.Pending
        });

        await fixture.Service.RefundPaymentAsync(NewRequest(100m));

        fixture.Refund.Status.Should().Be(PaymentStatus.Pending);
        fixture.Refunds.Verify(repository =>
            repository.UpdateAsync(fixture.Refund), Times.Once);
        fixture.Transaction.Status.Should().Be(PaymentStatus.Successful);
        fixture.Transactions.Verify(repository =>
            repository.UpdateAsync(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task RefundPaymentAsync_persists_rejected_provider_attempt_as_failed()
    {
        var fixture = CreateFixture(new RefundResponse
        {
            Success = false,
            Message = "rejected",
            Status = PaymentStatus.Failed
        });

        var response = await fixture.Service.RefundPaymentAsync(NewRequest(25m));

        response.Success.Should().BeFalse();
        fixture.Refund.Status.Should().Be(PaymentStatus.Failed);
        fixture.Refund.ProcessedAt.Should().NotBeNull();
        fixture.Refund.GatewayResponse.Should().Contain("rejected");
        fixture.Refunds.Verify(repository =>
            repository.UpdateAsync(fixture.Refund), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_marks_attempt_failed_when_gateway_throws()
    {
        var fixture = CreateFixture(exception: new HttpRequestException("network unavailable"));

        var action = () => fixture.Service.RefundPaymentAsync(NewRequest(25m));

        await action.Should().ThrowAsync<Exception>();
        fixture.Refund.Status.Should().Be(PaymentStatus.Failed);
        fixture.Refund.ProcessedAt.Should().NotBeNull();
        fixture.Refunds.Verify(repository =>
            repository.UpdateAsync(fixture.Refund), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_rejects_when_balance_cannot_be_reserved()
    {
        var fixture = CreateFixture(new RefundResponse { Success = true });
        fixture.Refunds
            .Setup(repository => repository.TryReserveAsync(
                It.IsAny<RefundTransaction>(),
                fixture.Transaction.Amount))
            .ReturnsAsync(false);

        var action = () => fixture.Service.RefundPaymentAsync(NewRequest(60m));

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*refundable balance*");
        fixture.Gateway.Verify(gateway =>
            gateway.RefundPaymentAsync(It.IsAny<RefundRequest>()), Times.Never);
    }

    private static Fixture CreateFixture(
        RefundResponse? response = null,
        Exception? exception = null)
    {
        var transaction = new PaymentTransaction
        {
            TransactionReference = "PAYMENT-1",
            Amount = 100m,
            Currency = "NGN",
            Status = PaymentStatus.Successful,
            Gateway = PaymentGatewayType.Flutterwave
        };
        var transactions = new Mock<ITransactionRepository>();
        transactions.Setup(repository => repository.GetByReferenceAsync("PAYMENT-1"))
            .ReturnsAsync(transaction);
        transactions.Setup(repository => repository.UpdateAsync(transaction))
            .ReturnsAsync(transaction);

        RefundTransaction? capturedRefund = null;
        var refunds = new Mock<IRefundRepository>();
        refunds.Setup(repository => repository.TryReserveAsync(
                It.IsAny<RefundTransaction>(),
                transaction.Amount))
            .Callback<RefundTransaction, decimal>((refund, _) => capturedRefund = refund)
            .ReturnsAsync(true);
        refunds.Setup(repository => repository.UpdateAsync(It.IsAny<RefundTransaction>()))
            .ReturnsAsync((RefundTransaction refund) => refund);
        refunds.Setup(repository => repository.GetByPaymentReferenceAsync("PAYMENT-1"))
            .ReturnsAsync(() => capturedRefund is null
                ? []
                : [capturedRefund]);

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.GatewayType).Returns(PaymentGatewayType.Flutterwave);
        if (exception is not null)
        {
            gateway.Setup(item => item.RefundPaymentAsync(It.IsAny<RefundRequest>()))
                .ThrowsAsync(exception);
        }
        else
        {
            gateway.Setup(item => item.RefundPaymentAsync(It.IsAny<RefundRequest>()))
                .ReturnsAsync(response!);
        }

        var service = new PaymentService(
            transactions.Object,
            refunds.Object,
            [gateway.Object],
            NullLogger<PaymentService>.Instance,
            new PaymentGatewayConfig());
        return new Fixture(
            service,
            transaction,
            transactions,
            refunds,
            gateway,
            () => capturedRefund!);
    }

    private static RefundRequest NewRequest(decimal amount) => new()
    {
        TransactionReference = "PAYMENT-1",
        Amount = amount,
        Reason = "requested_by_customer"
    };

    private sealed record Fixture(
        PaymentService Service,
        PaymentTransaction Transaction,
        Mock<ITransactionRepository> Transactions,
        Mock<IRefundRepository> Refunds,
        Mock<IPaymentGateway> Gateway,
        Func<RefundTransaction> GetRefund)
    {
        public RefundTransaction Refund => GetRefund();
    }
}
