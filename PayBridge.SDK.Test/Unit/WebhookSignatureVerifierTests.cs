using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Webhooks;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Helper;
using PayBridge.SDK.Services;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class WebhookSignatureVerifierTests
{
    private static readonly byte[] RawBody = Encoding.UTF8.GetBytes(
        "{\"event\":\"charge.success\",\"data\":{\"reference\":\"TXN_123\"}}");

    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(1_750_000_000);

    [Fact]
    public void Verify_accepts_a_valid_paystack_signature()
    {
        const string secret = "paystack-secret";
        var verifier = CreateVerifier(config => config.Paystack.SecretKey = secret);
        var request = CreateRequest(
            PaymentGatewayType.Paystack,
            ("x-paystack-signature", HmacHex(HashAlgorithmName.SHA512, secret, RawBody)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_a_missing_signature_without_throwing()
    {
        var verifier = CreateVerifier(config => config.Paystack.SecretKey = "paystack-secret");

        var result = verifier.Verify(CreateRequest(PaymentGatewayType.Paystack));

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.MissingSignature);
    }

    [Fact]
    public void Verify_rejects_an_invalid_signature()
    {
        var verifier = CreateVerifier(config => config.Paystack.SecretKey = "paystack-secret");
        var request = CreateRequest(PaymentGatewayType.Paystack, ("x-paystack-signature", "00"));

        var result = verifier.Verify(request);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.InvalidSignature);
    }

    [Fact]
    public void Verify_rejects_a_body_changed_after_signing()
    {
        const string secret = "paystack-secret";
        var verifier = CreateVerifier(config => config.Paystack.SecretKey = secret);
        var signature = HmacHex(HashAlgorithmName.SHA512, secret, RawBody);
        var changedBody = Encoding.UTF8.GetBytes(
            "{\"event\":\"charge.success\",\"data\":{\"reference\":\"CHANGED\"}}");
        var request = CreateRequest(
            PaymentGatewayType.Paystack,
            changedBody,
            ("x-paystack-signature", signature));

        var result = verifier.Verify(request);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.InvalidSignature);
    }

    [Fact]
    public void Verify_accepts_a_valid_flutterwave_signature()
    {
        const string secret = "flutterwave-webhook-secret";
        var verifier = CreateVerifier(config => config.FlutterwaveConfig.WebhookSecretHash = secret);
        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), RawBody));
        var request = CreateRequest(
            PaymentGatewayType.Flutterwave,
            ("flutterwave-signature", signature));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_accepts_a_valid_stripe_signature_within_the_replay_window()
    {
        const string secret = "whsec_test";
        var verifier = CreateVerifier(config => config.Stripe.WebhookSecret = secret);
        var timestamp = Now.ToUnixTimeSeconds();
        var signedPayload = Combine(Encoding.UTF8.GetBytes($"{timestamp}."), RawBody);
        var signature = HmacHex(HashAlgorithmName.SHA256, secret, signedPayload);
        var request = CreateRequest(
            PaymentGatewayType.Stripe,
            ("Stripe-Signature", $"t={timestamp},v1={signature}"));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_a_stale_stripe_signature()
    {
        const string secret = "whsec_test";
        var verifier = CreateVerifier(config => config.Stripe.WebhookSecret = secret);
        var timestamp = Now.AddMinutes(-6).ToUnixTimeSeconds();
        var signedPayload = Combine(Encoding.UTF8.GetBytes($"{timestamp}."), RawBody);
        var signature = HmacHex(HashAlgorithmName.SHA256, secret, signedPayload);
        var request = CreateRequest(
            PaymentGatewayType.Stripe,
            ("Stripe-Signature", $"t={timestamp},v1={signature}"));

        var result = verifier.Verify(request);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.TimestampOutsideTolerance);
    }

    [Fact]
    public void Verify_accepts_a_valid_checkout_signature()
    {
        const string secret = "checkout-webhook-secret";
        var verifier = CreateVerifier(config => config.Checkout.WebhookSecret = secret);
        var request = CreateRequest(
            PaymentGatewayType.Checkout,
            ("Cko-Signature", HmacHex(HashAlgorithmName.SHA256, secret, RawBody)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_accepts_a_valid_monnify_signature()
    {
        const string secret = "monnify-secret";
        var verifier = CreateVerifier(config => config.Monnify.SecretKey = secret);
        var request = CreateRequest(
            PaymentGatewayType.Monnify,
            ("monnify-signature", HmacHex(HashAlgorithmName.SHA512, secret, RawBody)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_accepts_a_valid_squad_signature()
    {
        const string secret = "squad-secret";
        var verifier = CreateVerifier(config => config.Squad.SecretKey = secret);
        var request = CreateRequest(
            PaymentGatewayType.Squad,
            ("x-squad-encrypted-body", HmacHex(HashAlgorithmName.SHA512, secret, RawBody)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_accepts_a_valid_korapay_data_signature()
    {
        const string secret = "korapay-secret";
        var rawBody = Encoding.UTF8.GetBytes(
            "{\"event\":\"charge.success\",\"data\":{\"reference\":\"KR_123\",\"amount\":1000}}");
        var signedData = Encoding.UTF8.GetBytes("{\"reference\":\"KR_123\",\"amount\":1000}");
        var verifier = CreateVerifier(config => config.Korapay.SecretKey = secret);
        var request = CreateRequest(
            PaymentGatewayType.Korapay,
            rawBody,
            ("x-korapay-signature", HmacHex(HashAlgorithmName.SHA256, secret, signedData)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_accepts_a_valid_peach_signature_within_the_replay_window()
    {
        const string secret = "peach-webhook-secret";
        const string requestUri = "https://example.test/api/webhook/PeachPayments";
        const string webhookId = "wh_123";
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var message = Encoding.UTF8.GetBytes(
            $"{timestamp}.{webhookId}.{requestUri}.{Encoding.UTF8.GetString(RawBody)}");
        var verifier = CreateVerifier(config => config.PeachPayments.WebhookSecret = secret);
        var request = CreateRequest(
            PaymentGatewayType.PeachPayments,
            RawBody,
            requestUri,
            ("x-webhook-signature-algorithm", "HMACSHA256"),
            ("x-webhook-timestamp", timestamp),
            ("x-webhook-id", webhookId),
            ("x-webhook-signature", HmacHex(HashAlgorithmName.SHA256, secret, message)));

        verifier.Verify(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_a_stale_peach_signature()
    {
        const string secret = "peach-webhook-secret";
        const string requestUri = "https://example.test/api/webhook/PeachPayments";
        const string webhookId = "wh_stale";
        var timestamp = Now.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var message = Encoding.UTF8.GetBytes(
            $"{timestamp}.{webhookId}.{requestUri}.{Encoding.UTF8.GetString(RawBody)}");
        var verifier = CreateVerifier(config => config.PeachPayments.WebhookSecret = secret);
        var request = CreateRequest(
            PaymentGatewayType.PeachPayments,
            RawBody,
            requestUri,
            ("x-webhook-signature-algorithm", "HMACSHA256"),
            ("x-webhook-timestamp", timestamp),
            ("x-webhook-id", webhookId),
            ("x-webhook-signature", HmacHex(HashAlgorithmName.SHA256, secret, message)));

        var result = verifier.Verify(request);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.TimestampOutsideTolerance);
    }

    [Fact]
    public void Verify_rejects_a_provider_without_a_supported_signature_contract()
    {
        var verifier = CreateVerifier();

        var result = verifier.Verify(CreateRequest(PaymentGatewayType.DpoGroup));

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(WebhookVerificationFailure.UnsupportedGateway);
    }

    [Theory]
    [InlineData("{\"TransactionRef\":\"SQ_CARD_1\"}", "SQ_CARD_1")]
    [InlineData("{\"Body\":{\"transaction_ref\":\"SQ_CARD_2\"}}", "SQ_CARD_2")]
    public void ExtractReference_supports_documented_squad_card_payloads(
        string json,
        string expected)
    {
        using var document = JsonDocument.Parse(json);

        var reference = GatewayExtractor.ExtractReferenceFromWebhook(
            document.RootElement,
            PaymentGatewayType.Squad);

        reference.Should().Be(expected);
    }

    [Fact]
    public void ExtractReference_supports_peach_form_fields()
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["merchantTransactionId"] = "PEACH_123"
        };

        var reference = GatewayExtractor.ExtractReferenceFromWebhook(
            fields,
            PaymentGatewayType.PeachPayments);

        reference.Should().Be("PEACH_123");
    }

    private static WebhookSignatureVerifier CreateVerifier(
        Action<PaymentGatewayConfig>? configure = null)
    {
        var config = new PaymentGatewayConfig();
        configure?.Invoke(config);
        return new WebhookSignatureVerifier(config, new FixedTimeProvider(Now));
    }

    private static WebhookVerificationRequest CreateRequest(
        PaymentGatewayType gateway,
        params (string Name, string Value)[] headers)
    {
        return CreateRequest(gateway, RawBody, "https://example.test/webhook", headers);
    }

    private static WebhookVerificationRequest CreateRequest(
        PaymentGatewayType gateway,
        byte[] rawBody,
        params (string Name, string Value)[] headers)
    {
        return CreateRequest(gateway, rawBody, "https://example.test/webhook", headers);
    }

    private static WebhookVerificationRequest CreateRequest(
        PaymentGatewayType gateway,
        byte[] rawBody,
        string requestUri,
        params (string Name, string Value)[] headers)
    {
        return new WebhookVerificationRequest(
            gateway,
            rawBody,
            headers.ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase),
            "POST",
            requestUri);
    }

    private static string HmacHex(
        HashAlgorithmName algorithm,
        string secret,
        byte[] payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = algorithm == HashAlgorithmName.SHA512
            ? HMACSHA512.HashData(key, payload)
            : HMACSHA256.HashData(key, payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] Combine(byte[] prefix, byte[] payload)
    {
        var combined = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, combined, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, combined, prefix.Length, payload.Length);
        return combined;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
