using System.Net;
using FluentAssertions;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

/// <summary>
/// Tests for the shared test infrastructure: MockHttpMessageHandler,
/// PaymentRequestFactory, GatewayConfigFactory, and IntegrationTestBase.
/// Closes #51.
/// </summary>
[Trait("Category", "Unit")]
public class TestInfrastructureTests
{
    // ── MockHttpMessageHandler ────────────────────────────────────────────────

    [Fact]
    public async Task MockHandler_ReturnsSingleQueuedResponse()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"status":true}""");

        var client = handler.BuildClient("https://api.example.com");

        var response = await client.GetAsync("/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":true");
    }

    [Fact]
    public async Task MockHandler_SupportsMultipleSequentialResponses()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"access_token":"tok123"}""")   // step 1 — auth
            .RespondWith(HttpStatusCode.OK, """{"status":true,"url":"https://pay.test"}"""); // step 2 — pay

        var client = handler.BuildClient("https://api.example.com");

        var auth = await client.PostAsync("/auth", null);
        var pay  = await client.PostAsync("/pay", null);

        handler.Requests.Should().HaveCount(2);
        (await auth.Content.ReadAsStringAsync()).Should().Contain("access_token");
        (await pay.Content.ReadAsStringAsync()).Should().Contain("url");
    }

    [Fact]
    public async Task MockHandler_Throws_WhenQueueIsEmpty()
    {
        var handler = new MockHttpMessageHandler(); // no responses queued
        var client = handler.BuildClient("https://api.example.com");

        var act = async () => await client.GetAsync("/unexpected");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no queued response*");
    }

    [Fact]
    public async Task MockHandler_RecordsRequests()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.Created, """{"id":"abc"}""");

        var client = handler.BuildClient("https://api.example.com");

        await client.PostAsync("/payments", null);

        handler.Requests.Should().HaveCount(1);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain("/payments");
    }

    [Fact]
    public async Task MockHandler_AssertRequestCount_Passes_ForCorrectCount()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, "{}")
            .RespondWith(HttpStatusCode.OK, "{}");

        var client = handler.BuildClient("https://api.example.com");

        await client.GetAsync("/a");
        await client.GetAsync("/b");

        var act = () => handler.AssertRequestCount(2);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task MockHandler_AssertRequestCount_Fails_ForWrongCount()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, "{}");

        var client = handler.BuildClient("https://api.example.com");
        await client.GetAsync("/a");

        var act = () => handler.AssertRequestCount(2);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Expected 2*got 1*");
    }

    [Fact]
    public async Task MockHandler_AssertLastRequestPath_Passes_WhenPathMatches()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, "{}");

        await handler.BuildClient("https://api.example.com").GetAsync("/payments/verify");

        var act = () => handler.AssertLastRequestPath("/payments/verify");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task MockHandler_RespondWithText_SetsCorrectContentType()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWithText(HttpStatusCode.OK, "<Result>000</Result>", "text/xml");

        var client = handler.BuildClient("https://api.example.com");
        var response = await client.PostAsync("/xml", null);

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/xml");
        (await response.Content.ReadAsStringAsync()).Should().Contain("000");
    }

    // ── PaymentRequestFactory ─────────────────────────────────────────────────

    [Fact]
    public void PaymentRequestFactory_Build_ReturnsValidRequest()
    {
        var request = PaymentRequestFactory.Build();

        request.Amount.Should().BeGreaterThan(0);
        request.Currency.Should().NotBeNullOrWhiteSpace();
        request.CustomerEmail.Should().Contain("@");
        request.RedirectUrl.Should().StartWith("https://");
        request.Metadata.Should().NotBeEmpty();
    }

    [Fact]
    public void PaymentRequestFactory_Build_AllowsCustomisation()
    {
        var request = PaymentRequestFactory.Build(r =>
        {
            r.Amount   = 100m;
            r.Currency = "USD";
        });

        request.Amount.Should().Be(100m);
        request.Currency.Should().Be("USD");
        request.CustomerEmail.Should().Contain("@"); // default still set
    }

    [Fact]
    public void PaymentRequestFactory_BuildRefund_ReturnsValidRequest()
    {
        var refund = PaymentRequestFactory.BuildRefund();

        refund.TransactionReference.Should().NotBeNullOrWhiteSpace();
        refund.Amount.Should().BeGreaterThan(0);
        refund.Reason.Should().NotBeNullOrWhiteSpace();
    }

    // ── GatewayConfigFactory ──────────────────────────────────────────────────

    [Fact]
    public void GatewayConfigFactory_BuildPaystack_SetsSecretKey()
    {
        var config = GatewayConfigFactory.BuildPaystack("sk_test_abc");
        config.Paystack.SecretKey.Should().Be("sk_test_abc");
    }

    [Fact]
    public void GatewayConfigFactory_BuildMonnify_SetsAllRequiredFields()
    {
        var config = GatewayConfigFactory.BuildMonnify("key", "secret", "contract");

        config.Monnify.ApiKey.Should().Be("key");
        config.Monnify.SecretKey.Should().Be("secret");
        config.Monnify.ContractCode.Should().Be("contract");
    }

    [Fact]
    public void GatewayConfigFactory_BuildSquad_DefaultsToSandbox()
    {
        var config = GatewayConfigFactory.BuildSquad();
        config.Squad.IsSandbox.Should().BeTrue();
    }

    [Fact]
    public void GatewayConfigFactory_BuildInterswitch_SetsClientCredentials()
    {
        var config = GatewayConfigFactory.BuildInterswitch("id", "secret");

        config.Interswitch.ClientId.Should().Be("id");
        config.Interswitch.ClientSecret.Should().Be("secret");
    }

    [Fact]
    public void GatewayConfigFactory_BuildPeachPayments_SetsEntityIdAndToken()
    {
        var config = GatewayConfigFactory.BuildPeachPayments("ent123", "tok456");

        config.PeachPayments.EntityId.Should().Be("ent123");
        config.PeachPayments.AccessToken.Should().Be("tok456");
    }

    [Theory]
    [InlineData("NGN")]
    [InlineData("USD")]
    [InlineData("KES")]
    [InlineData("GHS")]
    public void PaymentRequestFactory_Build_SupportsDifferentCurrencies(string currency)
    {
        var request = PaymentRequestFactory.Build(r => r.Currency = currency);
        request.Currency.Should().Be(currency);
    }
}
