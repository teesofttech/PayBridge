using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Controllers;
using PayBridge.SDK.Example.Services;
using PayBridge.SDK.Interfaces;
using PayBridge.SDK.Services;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class SecureWebhookControllerTests
{
    private const string Secret = "paystack-secret";
    private const string JsonBody =
        "{\"event\":\"charge.success\",\"data\":{\"reference\":\"TXN_123\"}}";

    [Fact]
    public async Task Receive_rejects_a_missing_signature_without_calling_payment_verification()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);
        var controller = CreateController(payment.Object);

        var result = await controller.Receive(PaymentGatewayType.Paystack, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        payment.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Receive_preserves_the_raw_body_and_processes_a_valid_signature()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);
        payment
            .Setup(service => service.VerifyPaymentAsync("TXN_123", PaymentGatewayType.Paystack))
            .ReturnsAsync(new VerificationResponse
            {
                Success = true,
                Status = PaymentStatus.Successful,
                TransactionReference = "TXN_123"
            });
        var rawBody = Encoding.UTF8.GetBytes(JsonBody);
        var signature = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), rawBody))
            .ToLowerInvariant();
        var controller = CreateController(payment.Object, signature);

        var result = await controller.Receive(PaymentGatewayType.Paystack, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        payment.VerifyAll();
    }

    private static WebhookController CreateController(
        IPaymentService paymentService,
        string? signature = null)
    {
        var config = new PaymentGatewayConfig();
        config.Paystack.SecretKey = Secret;
        var verifier = new WebhookSignatureVerifier(config, TimeProvider.System);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.Path = "/api/webhook/Paystack";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonBody));
        if (signature is not null)
        {
            context.Request.Headers["x-paystack-signature"] = signature;
        }

        return new WebhookController(
            paymentService,
            verifier,
            new OrderService(),
            new WebhookReplayStore(),
            NullLogger<WebhookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }
}
