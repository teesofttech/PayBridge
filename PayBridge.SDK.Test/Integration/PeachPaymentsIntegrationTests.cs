using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Integration;

/// <summary>
/// Integration tests for PeachPaymentsGateway against the real sandbox.
/// Requires env vars: PEACH_ENTITY_ID, PEACH_ACCESS_TOKEN
/// Skip automatically when env vars are not set.
/// Closes #50 (integration test portion).
/// </summary>
[Trait("Category", "Integration")]
public class PeachPaymentsIntegrationTests : IntegrationTestBase
{
    private readonly PeachPaymentsGateway? _gateway;

    public PeachPaymentsIntegrationTests()
        : base("PEACH_ENTITY_ID", "PEACH_ACCESS_TOKEN")
    {
        if (ShouldSkip) return;

        var config = new PaymentGatewayConfig
        {
            PeachPayments = new PeachPaymentsConfig
            {
                EntityId    = GetRequiredEnv("PEACH_ENTITY_ID"),
                AccessToken = GetRequiredEnv("PEACH_ACCESS_TOKEN"),
                IsSandbox   = true
            }
        };

        var httpClientFactory = new RealHttpClientFactory();
        _gateway = new PeachPaymentsGateway(config, httpClientFactory, NullLogger<PeachPaymentsGateway>.Instance);
    }

    [SkippableFact]
    public async Task CreatePaymentAsync_ReturnsSandboxCheckoutUrl()
    {
        SkipIfMissingEnvVars();

        var request = PaymentRequestFactory.Build(r =>
        {
            r.Amount        = 10.00m;
            r.Currency      = "ZAR";
            r.CustomerEmail = "sandbox@paybridge.dev";
            r.CustomerName  = "Sandbox Test";
        });

        var result = await _gateway!.CreatePaymentAsync(request);

        result.Should().NotBeNull();
        result.TransactionReference.Should().StartWith("PEACH_");

        // Peach sandbox returns 000.200.100 for a valid pending checkout
        result.Success.Should().BeTrue(
            because: $"expected sandbox success but got: {result.Message}");

        result.CheckoutUrl.Should().Contain("paymentWidgets.js");
    }

    [SkippableFact]
    public async Task VerifyPaymentAsync_ReturnsResponse_ForKnownSandboxReference()
    {
        SkipIfMissingEnvVars();

        // First create a payment to get a real reference
        var createRequest = PaymentRequestFactory.Build(r =>
        {
            r.Amount   = 10.00m;
            r.Currency = "ZAR";
        });

        var created = await _gateway!.CreatePaymentAsync(createRequest);
        created.Success.Should().BeTrue(because: "need a valid sandbox payment to verify");

        // Then verify it — it will be Pending as no card was entered
        var result = await _gateway.VerifyPaymentAsync(created.TransactionReference);

        result.Should().NotBeNull();
        result.TransactionReference.Should().Be(created.TransactionReference);
        result.Status.Should().BeOneOf(
            PaymentStatus.Pending, PaymentStatus.Successful, PaymentStatus.Failed);
    }
}

/// <summary>
/// Real HttpClientFactory for integration tests — uses a live HttpClient.
/// </summary>
internal sealed class RealHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name = "") => new HttpClient();
}
