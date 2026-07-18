using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class FlutterwaveGatewayRefundTests
{
    [Theory]
    [InlineData(1000, "full")]
    [InlineData(250.50, "partial")]
    public async Task RefundPaymentAsync_uses_provider_transaction_id_and_explicit_amount(
        decimal amount,
        string refundType)
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, VerificationJson(amount: 1000))
            .RespondWith(HttpStatusCode.OK, RefundJson(amount, "completed"));
        var gateway = CreateGateway(new MockHttpClientFactory(handler));
        var request = PaymentRequestFactory.BuildRefund(value =>
        {
            value.TransactionReference = "FLW_MERCHANT_REF";
            value.Amount = amount;
            value.Reason = "requested_by_customer";
        });

        var result = await gateway.RefundPaymentAsync(request);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(PaymentStatus.Refunded);
        result.RefundReference.Should().Be("URF_REFUND_123");
        result.TransactionReference.Should().Be("FLW_MERCHANT_REF");
        result.Amount.Should().Be(amount);
        result.Message.Should().Contain(refundType);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].RequestUri!.PathAndQuery.Should()
            .Be("/v3/transactions/verify_by_reference?tx_ref=FLW_MERCHANT_REF");
        handler.Requests[1].RequestUri!.AbsolutePath.Should()
            .Be("/v3/transactions/987654/refund");
        using var payload = JsonDocument.Parse(
            handler.RequestBodies[1]!);
        payload.RootElement.GetProperty("amount").GetDecimal().Should().Be(amount);
        payload.RootElement.GetProperty("comments").GetString().Should()
            .Be("requested_by_customer");
    }

    [Fact]
    public async Task RefundPaymentAsync_maps_rejected_response()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, VerificationJson())
            .RespondWith(HttpStatusCode.BadRequest,
                """{"status":"error","message":"Amount should be above NGN100","data":null}""");
        var gateway = CreateGateway(new MockHttpClientFactory(handler));

        var result = await gateway.RefundPaymentAsync(PaymentRequestFactory.BuildRefund());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.Message.Should().Be("Amount should be above NGN100");
    }

    [Fact]
    public async Task RefundPaymentAsync_maps_malformed_response_without_throwing()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, VerificationJson())
            .RespondWith(HttpStatusCode.OK, "not-json", "text/plain");
        var gateway = CreateGateway(new MockHttpClientFactory(handler));

        var result = await gateway.RefundPaymentAsync(PaymentRequestFactory.BuildRefund());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.Message.Should().Contain("invalid response");
    }

    [Fact]
    public async Task RefundPaymentAsync_maps_timeout_without_throwing()
    {
        var gateway = CreateGateway(new StubHttpClientFactory(
            new TimeoutHandler(VerificationJson())));

        var result = await gateway.RefundPaymentAsync(PaymentRequestFactory.BuildRefund());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.Message.Should().Contain("timed out");
    }

    [Fact]
    public async Task RefundPaymentAsync_rejects_amount_above_verified_payment()
    {
        var handler = new MockHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, VerificationJson(amount: 500));
        var gateway = CreateGateway(new MockHttpClientFactory(handler));

        var result = await gateway.RefundPaymentAsync(
            PaymentRequestFactory.BuildRefund(value => value.Amount = 501));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("exceeds");
        handler.Requests.Should().ContainSingle();
    }

    private static FlutterwaveGateway CreateGateway(IHttpClientFactory factory)
    {
        var config = new PaymentGatewayConfig
        {
            FlutterwaveConfig = new FlutterwaveConfig { SecretKey = "FLW_TEST_SECRET" }
        };
        return new FlutterwaveGateway(
            Options.Create(config),
            NullLogger<FlutterwaveGateway>.Instance,
            factory);
    }

    private static string VerificationJson(decimal amount = 5000) => JsonSerializer.Serialize(
        new
        {
            status = "success",
            message = "Transaction fetched",
            data = new
            {
                id = 987654,
                tx_ref = "FLW_MERCHANT_REF",
                amount,
                currency = "NGN",
                status = "successful"
            }
        });

    private static string RefundJson(decimal amount, string status) => JsonSerializer.Serialize(
        new
        {
            status = "success",
            message = "Transaction refund initiated",
            data = new
            {
                id = 75923,
                flw_ref = "URF_REFUND_123",
                amount_refunded = amount,
                status,
                created_at = "2026-07-18T03:00:00Z"
            }
        });

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "") => new(handler);
    }

    private sealed class TimeoutHandler(string verificationJson) : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestCount++;
            if (_requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(verificationJson)
                });
            }

            throw new TaskCanceledException("The request timed out");
        }
    }
}
