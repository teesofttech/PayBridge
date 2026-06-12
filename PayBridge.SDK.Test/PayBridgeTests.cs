using FluentAssertions;
using PayBridge.SDK.Enums;
using Xunit;

namespace PayBridge.SDK.Test;

/// <summary>
/// Smoke tests to verify the test infrastructure is wired up correctly.
/// Full gateway unit and integration tests are in their respective test files.
/// </summary>
public class PayBridgeTests
{
    [Fact]
    public void PaymentGatewayType_ShouldContainAllExpectedGateways()
    {
        var gatewayTypes = Enum.GetValues<PaymentGatewayType>().ToList();

        gatewayTypes.Should().Contain(PaymentGatewayType.Flutterwave);
        gatewayTypes.Should().Contain(PaymentGatewayType.Paystack);
        gatewayTypes.Should().Contain(PaymentGatewayType.Stripe);
        gatewayTypes.Should().Contain(PaymentGatewayType.Checkout);
        gatewayTypes.Should().Contain(PaymentGatewayType.BenefitPay);
        gatewayTypes.Should().Contain(PaymentGatewayType.Knet);
        gatewayTypes.Should().Contain(PaymentGatewayType.Monnify);
        gatewayTypes.Should().Contain(PaymentGatewayType.Squad);
        gatewayTypes.Should().Contain(PaymentGatewayType.Korapay);
        gatewayTypes.Should().Contain(PaymentGatewayType.Interswitch);
        gatewayTypes.Should().Contain(PaymentGatewayType.Remita);
        gatewayTypes.Should().Contain(PaymentGatewayType.Opay);
        gatewayTypes.Should().Contain(PaymentGatewayType.DpoGroup);
        gatewayTypes.Should().Contain(PaymentGatewayType.PawaPay);
        gatewayTypes.Should().Contain(PaymentGatewayType.PeachPayments);
    }

    [Fact]
    public void PaymentGatewayType_Automatic_ShouldHaveValueZero()
    {
        ((int)PaymentGatewayType.Automatic).Should().Be(0);
    }

    [Theory]
    [InlineData(PaymentGatewayType.Monnify, 7)]
    [InlineData(PaymentGatewayType.Squad, 8)]
    [InlineData(PaymentGatewayType.Korapay, 9)]
    [InlineData(PaymentGatewayType.Interswitch, 10)]
    [InlineData(PaymentGatewayType.Remita, 11)]
    [InlineData(PaymentGatewayType.Opay, 12)]
    [InlineData(PaymentGatewayType.DpoGroup, 13)]
    [InlineData(PaymentGatewayType.PawaPay, 14)]
    [InlineData(PaymentGatewayType.PeachPayments, 15)]
    public void PaymentGatewayType_NewGateways_ShouldHaveCorrectEnumValues(
        PaymentGatewayType gateway, int expectedValue)
    {
        ((int)gateway).Should().Be(expectedValue);
    }
}