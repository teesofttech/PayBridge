using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

/// <summary>
/// Unit tests for PeachPaymentsGateway.
/// All HTTP calls are intercepted by MockHttpMessageHandler — no live network.
/// Closes #50 (unit test portion).
/// </summary>
[Trait("Category", "Unit")]
public class PeachPaymentsGatewayTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PeachPaymentsGateway BuildGateway(
        MockHttpMessageHandler handler,
        string entityId    = "test_entity_id",
        string accessToken = "test_access_token",
        bool isSandbox     = true)
    {
        var config = GatewayConfigFactory.BuildPeachPayments(entityId, accessToken, isSandbox);
        var factory = new MockHttpClientFactory(handler);
        var logger  = NullLogger<PeachPaymentsGateway>.Instance;
        return new PeachPaymentsGateway(config, factory, logger);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        var act = () => new PeachPaymentsGateway(
            null!,
            new MockHttpClientFactory(new MockHttpMessageHandler()),
            NullLogger<PeachPaymentsGateway>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var config = GatewayConfigFactory.BuildPeachPayments();
        var act = () => new PeachPaymentsGateway(
            config,
            new MockHttpClientFactory(new MockHttpMessageHandler()),
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Succeeds_WithValidConfig()
    {
        var handler = new MockHttpMessageHandler();
        var act = () => BuildGateway(handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void GatewayType_Returns_PeachPayments()
    {
        var gateway = BuildGateway(new MockHttpMessageHandler());
        gateway.GatewayType.Should().Be(PaymentGatewayType.PeachPayments);
    }

    // ── CreatePaymentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentAsync_ReturnsSuccess_WithCheckoutUrl_OnSuccessResponse()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_abc123","result":{"code":"000.200.100","description":"Transaction pending"}}""");

        var gateway  = BuildGateway(handler);
        var request  = PaymentRequestFactory.Build(r => r.Currency = "ZAR");

        var result = await gateway.CreatePaymentAsync(request);

        result.Success.Should().BeTrue();
        result.CheckoutUrl.Should().Contain("chk_abc123");
        result.TransactionReference.Should().StartWith("PEACH_");
        result.Status.Should().Be(PaymentStatus.Pending);
        result.GatewayResponse["checkoutId"].Should().Be("chk_abc123");
        result.GatewayResponse["resultCode"].Should().Be("000.200.100");
    }

    [Fact]
    public async Task CreatePaymentAsync_PostsToCheckoutsEndpoint()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"000.200.100","description":"pending"}}""");

        var gateway = BuildGateway(handler);
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        handler.AssertLastRequestPath("/checkouts");
        handler.AssertLastMethod(HttpMethod.Post);
    }

    [Fact]
    public async Task CreatePaymentAsync_SetsAuthorizationBearerHeader()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler, accessToken: "my_access_token");
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("my_access_token");
    }

    [Fact]
    public async Task CreatePaymentAsync_UsesEntityId_InFormBody()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler, entityId: "my_entity_id");
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        var bodyContent = await handler.LastRequest!.Content!.ReadAsStringAsync();
        bodyContent.Should().Contain("entityId=my_entity_id");
    }

    [Fact]
    public async Task CreatePaymentAsync_ReturnsFailure_WhenApiReturnsNonSuccessCode()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.BadRequest,
            """{"result":{"code":"800.100.100","description":"Invalid credentials"}}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task CreatePaymentAsync_ReturnsFailure_WhenResultCodeDoesNotStartWith000()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"800.100.100","description":"Rejected"}}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    [Theory]
    [InlineData("ZAR", "ZA")]
    [InlineData("KES", "KE")]
    [InlineData("NGN", "NG")]
    [InlineData("BWP", "BW")]
    [InlineData("USD", "ZA")] // unknown defaults to ZA
    public async Task CreatePaymentAsync_MapsCorrectBillingCountry_ForCurrency(
        string currency, string expectedCountry)
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler);
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build(r => r.Currency = currency));

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain($"billing.country={expectedCountry}");
    }

    [Fact]
    public async Task CreatePaymentAsync_FormatsAmount_WithTwoDecimalPlaces()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_001","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler);
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build(r => r.Amount = 1500.5m));

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("amount=1500.50");
    }

    [Fact]
    public async Task CreatePaymentAsync_UsesSandboxUrl_WhenIsSandboxTrue()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_s","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler, isSandbox: true);
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        handler.LastRequest!.RequestUri!.ToString().Should().Contain("eu-test.oppwa.com");
    }

    [Fact]
    public async Task CreatePaymentAsync_UsesLiveUrl_WhenIsSandboxFalse()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"chk_l","result":{"code":"000.200.100","description":"ok"}}""");

        var gateway = BuildGateway(handler, isSandbox: false);
        await gateway.CreatePaymentAsync(PaymentRequestFactory.Build());

        handler.LastRequest!.RequestUri!.ToString().Should().Contain("eu-prod.oppwa.com");
    }

    // ── VerifyPaymentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyPaymentAsync_ReturnsSuccess_WhenResultCodeStartsWith000_0()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"result":{"code":"000.000.000","description":"Transaction succeeded"},"amount":"1500.00","currency":"ZAR","paymentBrand":"VISA"}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.VerifyPaymentAsync("PEACH_ref_001");

        result.Success.Should().BeTrue();
        result.TransactionReference.Should().Be("PEACH_ref_001");
        result.Amount.Should().Be(1500.00m);
        result.Currency.Should().Be("ZAR");
        result.PaymentMethod.Should().Be("VISA");
        result.Status.Should().Be(PaymentStatus.Successful);
    }

    [Fact]
    public async Task VerifyPaymentAsync_ReturnsSuccess_WhenResultCodeStartsWith000_100()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"result":{"code":"000.100.110","description":"Request successfully processed"},"amount":"500.00","currency":"KES","paymentBrand":"MASTERCARD"}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.VerifyPaymentAsync("PEACH_ref_002");

        result.Success.Should().BeTrue();
        result.Status.Should().Be(PaymentStatus.Successful);
        result.GatewayResponse["resultCode"].Should().Be("000.100.110");
    }

    [Fact]
    public async Task VerifyPaymentAsync_ReturnsFailure_WhenResultCodeDoesNotMatch()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"result":{"code":"800.100.100","description":"Invalid payment data"},"amount":"0","currency":"ZAR","paymentBrand":"VISA"}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.VerifyPaymentAsync("PEACH_ref_003");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task VerifyPaymentAsync_ReturnsFailure_WhenApiReturnsNonSuccess()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.Unauthorized,
            """{"result":{"code":"800.900.100","description":"Unauthorized"}}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.VerifyPaymentAsync("PEACH_ref_004");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task VerifyPaymentAsync_SendsGetRequest_WithEntityIdAndReference()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"result":{"code":"000.000.000","description":"ok"},"amount":"100.00","currency":"ZAR","paymentBrand":"VISA"}""");

        var gateway = BuildGateway(handler, entityId: "ent_xyz");
        await gateway.VerifyPaymentAsync("PEACH_test_ref");

        handler.AssertLastMethod(HttpMethod.Get);
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("entityId=ent_xyz");
        url.Should().Contain("merchantTransactionId=PEACH_test_ref");
    }

    // ── RefundPaymentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RefundPaymentAsync_ReturnsSuccess_WhenResultCodeStartsWith000()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"ref_abc","result":{"code":"000.100.110","description":"Refund processed"}}""");

        var gateway = BuildGateway(handler);
        var request = PaymentRequestFactory.BuildRefund(r =>
        {
            r.TransactionReference = "PEACH_orig_001";
            r.Amount = 500.00m;
        });

        var result = await gateway.RefundPaymentAsync(request);

        result.Success.Should().BeTrue();
        result.RefundReference.Should().Be("ref_abc");
        result.TransactionReference.Should().Be("PEACH_orig_001");
        result.Amount.Should().Be(500.00m);
        result.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task RefundPaymentAsync_ReturnsFailure_WhenResultCodeDoesNotStartWith000()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"","result":{"code":"700.400.100","description":"Refund rejected"}}""");

        var gateway = BuildGateway(handler);
        var result  = await gateway.RefundPaymentAsync(PaymentRequestFactory.BuildRefund());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task RefundPaymentAsync_PostsToCorrectEndpoint()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"ref_001","result":{"code":"000.100.110","description":"ok"}}""");

        var gateway = BuildGateway(handler);
        var request = PaymentRequestFactory.BuildRefund(r => r.TransactionReference = "PEACH_orig_999");

        await gateway.RefundPaymentAsync(request);

        handler.AssertLastMethod(HttpMethod.Post);
        handler.AssertLastRequestPath("PEACH_orig_999");
    }

    [Fact]
    public async Task RefundPaymentAsync_FormatsAmount_WithTwoDecimalPlaces()
    {
        var handler = new MockHttpMessageHandler().RespondWith(
            HttpStatusCode.OK,
            """{"id":"ref_001","result":{"code":"000.100.110","description":"ok"}}""");

        var gateway = BuildGateway(handler);
        await gateway.RefundPaymentAsync(PaymentRequestFactory.BuildRefund(r => r.Amount = 250.5m));

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("amount=250.50");
    }
}
