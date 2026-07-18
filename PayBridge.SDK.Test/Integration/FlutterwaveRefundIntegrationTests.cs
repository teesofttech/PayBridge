using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Integration;

/// <summary>
/// Opt-in Flutterwave sandbox refund test. Use a dedicated, successful sandbox
/// transaction that can be refunded exactly once; never provide live credentials.
/// </summary>
[Trait("Category", "Integration")]
public class FlutterwaveRefundIntegrationTests : IntegrationTestBase
{
    private readonly FlutterwaveGateway? _gateway;
    private readonly string? _transactionReference;
    private readonly decimal _amount;

    public FlutterwaveRefundIntegrationTests()
        : base(
            "FLUTTERWAVE_SECRET_KEY",
            "FLUTTERWAVE_REFUND_TRANSACTION_REFERENCE",
            "FLUTTERWAVE_REFUND_AMOUNT")
    {
        if (ShouldSkip)
        {
            return;
        }

        _transactionReference = GetRequiredEnv("FLUTTERWAVE_REFUND_TRANSACTION_REFERENCE");
        _amount = decimal.Parse(
            GetRequiredEnv("FLUTTERWAVE_REFUND_AMOUNT"),
            System.Globalization.CultureInfo.InvariantCulture);
        var config = new PaymentGatewayConfig
        {
            FlutterwaveConfig = new FlutterwaveConfig
            {
                SecretKey = GetRequiredEnv("FLUTTERWAVE_SECRET_KEY")
            }
        };
        _gateway = new FlutterwaveGateway(
            Options.Create(config),
            NullLogger<FlutterwaveGateway>.Instance,
            new RealHttpClientFactory());
    }

    [SkippableFact]
    public async Task RefundPaymentAsync_RefundsDedicatedSandboxTransaction()
    {
        SkipIfMissingEnvVars();

        var result = await _gateway!.RefundPaymentAsync(
            PaymentRequestFactory.BuildRefund(request =>
            {
                request.TransactionReference = _transactionReference!;
                request.Amount = _amount;
                request.Reason = "requested_by_customer";
            }));

        result.Success.Should().BeTrue(because: result.Message);
        result.RefundReference.Should().NotBeNullOrWhiteSpace();
        result.Amount.Should().Be(_amount);
    }
}
