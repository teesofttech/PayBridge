using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class PaymentServiceRoutingTests
{
    [Fact]
    public async Task Automatic_routing_honors_compatible_configured_default()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var checkout = Gateway(PaymentGatewayType.Checkout);
        var service = CreateService(
            new PaymentGatewayConfig { DefaultGateway = PaymentGatewayType.Checkout },
            stripe.Object,
            checkout.Object);

        await service.CreatePaymentAsync(Request("USD"));

        checkout.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Automatic_routing_is_deterministic_across_registration_order()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var checkout = Gateway(PaymentGatewayType.Checkout);
        var service = CreateService(new PaymentGatewayConfig(), checkout.Object, stripe.Object);

        await service.CreatePaymentAsync(Request("USD"));

        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
        checkout.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Incompatible_default_is_skipped_for_compatible_gateway()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var paystack = Gateway(PaymentGatewayType.Paystack);
        var service = CreateService(
            new PaymentGatewayConfig { DefaultGateway = PaymentGatewayType.Paystack },
            paystack.Object,
            stripe.Object);

        await service.CreatePaymentAsync(Request("USD"));

        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
        paystack.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Single_incompatible_gateway_does_not_bypass_routing_rules()
    {
        var checkout = Gateway(PaymentGatewayType.Checkout);
        var service = CreateService(new PaymentGatewayConfig(), checkout.Object);

        var action = () => service.CreatePaymentAsync(Request("NGN"));

        await action.Should().ThrowAsync<Exception>().WithMessage("*supports NGN*");
        checkout.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Theory]
    [InlineData("ZZZ", PaymentMethodType.Card)]
    [InlineData("USD", PaymentMethodType.Crypto)]
    [InlineData("NGN", PaymentMethodType.BankTransfer)]
    [InlineData("KES", PaymentMethodType.MobileMoney)]
    [InlineData("USD", PaymentMethodType.Wallet)]
    [InlineData("NGN", PaymentMethodType.Ussd)]
    [InlineData("NGN", PaymentMethodType.QrCode)]
    public async Task Unsupported_routes_fail_before_provider_call(
        string currency,
        PaymentMethodType method)
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var service = CreateService(new PaymentGatewayConfig(), stripe.Object);
        var request = Request(currency);
        request.PaymentMethodType = method;

        var action = () => service.CreatePaymentAsync(request);

        var exception = await action.Should().ThrowAsync<Exception>();
        var message = exception.Which.Message;
        (message.Contains("supports", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Specify a gateway explicitly", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Stripe_routes_documented_JPY_currency()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var service = CreateService(new PaymentGatewayConfig(), stripe.Object);

        await service.CreatePaymentAsync(Request("JPY"));

        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
    }

    [Fact]
    public async Task Automatic_routing_rejects_saved_payment_method_until_binding_is_implemented()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var service = CreateService(new PaymentGatewayConfig(), stripe.Object);
        var request = Request("USD");
        request.SavedPaymentMethodId = "pm_saved_123";

        var action = () => service.CreatePaymentAsync(request);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*Saved payment method routing is not yet implemented*");
        stripe.Verify(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task Verify_automatic_routing_rejects_unknown_reference_prefix()
    {
        var stripe = Gateway(PaymentGatewayType.Stripe);
        var transactionRepository = new Mock<ITransactionRepository>();
        transactionRepository.Setup(repository => repository.GetByReferenceAsync("UNKNOWN_123"))
            .ReturnsAsync((PayBridge.SDK.Entities.PaymentTransaction?)null);
        var service = new PaymentService(
            transactionRepository.Object,
            Mock.Of<IRefundRepository>(),
            [stripe.Object],
            NullLogger<PaymentService>.Instance,
            new PaymentGatewayConfig { DefaultGateway = PaymentGatewayType.Stripe });

        var action = () => service.VerifyPaymentAsync("UNKNOWN_123");

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*Unable to determine gateway from transaction reference*");
        stripe.Verify(item => item.VerifyPaymentAsync(It.IsAny<string>()), Times.Never);
    }

    private static PaymentService CreateService(
        PaymentGatewayConfig config,
        params IPaymentGateway[] gateways) =>
        new(
            Mock.Of<ITransactionRepository>(),
            Mock.Of<IRefundRepository>(),
            gateways,
            NullLogger<PaymentService>.Instance,
            config);

    private static Mock<IPaymentGateway> Gateway(PaymentGatewayType type)
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.GatewayType).Returns(type);
        gateway.Setup(item => item.CreatePaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResponse { Success = false, Status = PaymentStatus.Failed });
        gateway.Setup(item => item.VerifyPaymentAsync(It.IsAny<string>()))
            .ReturnsAsync(new VerificationResponse { Success = false, Status = PaymentStatus.Failed });
        return gateway;
    }

    private static PaymentRequest Request(string currency) => new()
    {
        Amount = 100m,
        Currency = currency,
        CustomerEmail = "customer@example.test"
    };
}
