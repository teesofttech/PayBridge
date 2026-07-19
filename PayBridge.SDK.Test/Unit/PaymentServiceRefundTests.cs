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
            repository.FinalizeAsync(It.IsAny<RefundTransaction>(), It.IsAny<RefundResponse>()), Times.Once);
        fixture.Transaction.Status.Should().Be(PaymentStatus.Refunded);
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
            repository.FinalizeAsync(It.IsAny<RefundTransaction>(), It.IsAny<RefundResponse>()), Times.Once);
        fixture.Transaction.Status.Should().Be(PaymentStatus.Successful);
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
            repository.FinalizeAsync(It.IsAny<RefundTransaction>(), It.IsAny<RefundResponse>()), Times.Once);
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
            repository.FinalizeAsync(It.IsAny<RefundTransaction>(), It.IsAny<RefundResponse>()), Times.Once);
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

    [Fact]
    public async Task RefundPaymentAsync_replays_stored_response_for_same_idempotency_request()
    {
        var fixture = CreateFixture(new RefundResponse { Success = true });
        var stored = new RefundResponse
        {
            Success = true,
            RefundReference = "stored-refund",
            TransactionReference = "PAYMENT-1",
            Message = "already processed",
            Amount = 40m,
            Status = PaymentStatus.Refunded,
            RefundDate = DateTime.UtcNow.AddMinutes(-2)
        };

        fixture.Refunds
            .Setup(repository => repository.GetByIdempotencyKeyAsync("refund-key-1"))
            .ReturnsAsync(new RefundTransaction
            {
                Id = "refund-1",
                IdempotencyKey = "refund-key-1",
                PaymentTransactionReference = "PAYMENT-1",
                Amount = 40m,
                Currency = "NGN",
                Status = PaymentStatus.Refunded,
                RequestFingerprint = ComputeTestFingerprint(NewRequest(40m, "refund-key-1")),
                GatewayResponse = System.Text.Json.JsonSerializer.Serialize(stored),
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
                ProcessedAt = stored.RefundDate
            });

        var response = await fixture.Service.RefundPaymentAsync(NewRequest(40m, "refund-key-1"));

        response.RefundReference.Should().Be("stored-refund");
        response.Status.Should().Be(PaymentStatus.Refunded);
        fixture.Gateway.Verify(gateway =>
            gateway.RefundPaymentAsync(It.IsAny<RefundRequest>()), Times.Never);
    }

    [Fact]
    public async Task RefundPaymentAsync_rejects_reused_idempotency_key_with_different_payload()
    {
        var fixture = CreateFixture(new RefundResponse { Success = true });

        fixture.Refunds
            .Setup(repository => repository.GetByIdempotencyKeyAsync("refund-key-2"))
            .ReturnsAsync(new RefundTransaction
            {
                Id = "refund-2",
                IdempotencyKey = "refund-key-2",
                PaymentTransactionReference = "PAYMENT-1",
                Amount = 20m,
                Currency = "NGN",
                Status = PaymentStatus.Refunded,
                RequestFingerprint = ComputeTestFingerprint(NewRequest(20m, "refund-key-2")),
                CreatedAt = DateTime.UtcNow
            });

        var action = () => fixture.Service.RefundPaymentAsync(NewRequest(30m, "refund-key-2"));

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*different refund parameters*");
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
        refunds.Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>()))
            .ReturnsAsync((RefundTransaction?)null);
        refunds.Setup(repository => repository.UpdateAsync(It.IsAny<RefundTransaction>()))
            .ReturnsAsync((RefundTransaction refund) => refund);
        refunds.Setup(repository => repository.FinalizeAsync(
                It.IsAny<RefundTransaction>(),
                It.IsAny<RefundResponse>()))
            .ReturnsAsync((RefundTransaction refund, RefundResponse response) =>
            {
                refund.RefundReference = string.IsNullOrWhiteSpace(response.RefundReference)
                    ? refund.Id
                    : response.RefundReference;
                refund.Status = response.Success ? response.Status : PaymentStatus.Failed;
                refund.ProcessedAt = response.RefundDate == default ? DateTime.UtcNow : response.RefundDate;
                refund.GatewayResponse = response.Message;

                if (refund.Status == PaymentStatus.Refunded)
                {
                    transaction.Status = PaymentStatus.Refunded;
                }

                return refund;
            });
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

    private static RefundRequest NewRequest(decimal amount, string? idempotencyKey = null) => new()
    {
        TransactionReference = "PAYMENT-1",
        Amount = amount,
        Reason = "requested_by_customer",
        IdempotencyKey = idempotencyKey
    };

    private static string ComputeTestFingerprint(RefundRequest request)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            request.TransactionReference,
            request.Amount,
            Reason = request.Reason ?? string.Empty
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload)));
    }

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
