using FluentAssertions;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Helper;
using System.Dynamic;
using System.Text.Json;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

/// <summary>
/// Unit tests for GatewayExtractor — webhook detection and reference extraction.
/// Closes #52 (partial).
/// </summary>
[Trait("Category", "Unit")]
public class GatewayExtractorTests
{
    // ── DetectGatewayFromWebhook ──────────────────────────────────────────────

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsPaystack_WhenPayloadHasEventKey()
    {
        var payload = new Dictionary<string, object>
        {
            ["event"] = "charge.success",
            ["data"]  = new { reference = "PST_123" }
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsFlutterwave_WhenPayloadHasFlwRefKey()
    {
        var payload = new Dictionary<string, object>
        {
            ["flw_ref"] = "FLW-MOCK-abc123",
            ["tx_ref"]  = "FLW_txn"
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Flutterwave);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsStripe_ForEventEnvelope()
    {
        dynamic payload = new ExpandoObject();
        payload.type = "payment_intent.succeeded";
        payload.id   = "evt_123";
        payload.data = new { @object = new { id = "pi_123" } };

        PaymentGatewayType result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Stripe);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsCheckout_WhenPayloadHasLinksKey()
    {
        var payload = new Dictionary<string, object>
        {
            ["_links"] = new { self = new { href = "https://api.checkout.com/payments/pay_abc" } },
            ["id"]     = "pay_abc"
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Checkout);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsKorapay_WhenReferenceStartsWithKR_()
    {
        var payload = new Dictionary<string, object>
        {
            ["reference"] = "KR_test_abc123",
            ["status"]    = "success"
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Korapay);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsAutomatic_WhenPayloadIsUnrecognised()
    {
        var payload = new Dictionary<string, object>
        {
            ["unknown_field"] = "some_value",
            ["data"]          = "something"
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Automatic);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsAutomatic_WhenPayloadIsNotADictionary()
    {
        // Passing a non-dictionary object — should catch the cast exception and return Automatic
        var result = GatewayExtractor.DetectGatewayFromWebhook("invalid-payload");

        result.Should().Be(PaymentGatewayType.Automatic);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsAutomatic_WhenReferenceDoesNotMatchKnownPrefix()
    {
        var payload = new Dictionary<string, object>
        {
            ["reference"] = "UNKNOWN_PREFIX_abc123"
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Automatic);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsAutomatic_WhenPayloadIsNull()
    {
        var result = GatewayExtractor.DetectGatewayFromWebhook(null!);

        result.Should().Be(PaymentGatewayType.Automatic);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsPaystack_ForEventAndDataEnvelope()
    {
        var payload = new Dictionary<string, object>
        {
            ["event"] = "charge.success",
            ["type"]  = "stripe.some_event",
            ["data"] = new { reference = "PAY-1" }
        };

        var result = GatewayExtractor.DetectGatewayFromWebhook(payload);

        result.Should().Be(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsFlutterwave_ForV3PayloadContainingEvent()
    {
        using var document = JsonDocument.Parse("""
            {
              "event": "charge.completed",
              "data": {
                "tx_ref": "merchant-ref",
                "flw_ref": "FLW-MOCK-123"
              }
            }
            """);

        var result = GatewayExtractor.DetectGatewayFromWebhookResult(document.RootElement);

        result.Status.Should().Be(WebhookGatewayDetectionStatus.Detected);
        result.Gateway.Should().Be(PaymentGatewayType.Flutterwave);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReturnsFlutterwave_ForCurrentWebhookEnvelope()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "wbk_W5p6ktwU0jQ8RO4By860",
              "timestamp": 1735116884019,
              "type": "charge.completed",
              "data": { "reference": "merchant-ref", "status": "succeeded" }
            }
            """);

        var result = GatewayExtractor.DetectGatewayFromWebhookResult(document.RootElement);

        result.Status.Should().Be(WebhookGatewayDetectionStatus.Detected);
        result.Gateway.Should().Be(PaymentGatewayType.Flutterwave);
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReportsAmbiguousPayload()
    {
        var payload = new Dictionary<string, object>
        {
            ["event"] = "charge.success",
            ["data"] = new { reference = "PAY-1" },
            ["_links"] = new { self = new { href = "https://api.checkout.com/events/evt_1" } }
        };

        var result = GatewayExtractor.DetectGatewayFromWebhookResult(payload);

        result.Status.Should().Be(WebhookGatewayDetectionStatus.Ambiguous);
        result.Gateway.Should().BeNull();
    }

    [Fact]
    public void DetectGatewayFromWebhook_ReportsInvalidMalformedJson()
    {
        using var document = JsonDocument.Parse("[]");

        var result = GatewayExtractor.DetectGatewayFromWebhookResult(document.RootElement);

        result.Status.Should().Be(WebhookGatewayDetectionStatus.Invalid);
        result.Gateway.Should().BeNull();
    }

    [Fact]
    public void DetectGatewayFromWebhook_SupportsTypedPayloads()
    {
        var payload = new { @event = "charge.success", data = new { reference = "PAY-1" } };

        var result = GatewayExtractor.DetectGatewayFromWebhookResult(payload);

        result.Status.Should().Be(WebhookGatewayDetectionStatus.Detected);
        result.Gateway.Should().Be(PaymentGatewayType.Paystack);
    }

    // ── ExtractReferenceFromWebhook ───────────────────────────────────────────

    [Fact]
    public void ExtractReferenceFromWebhook_ReturnsRef_ForKorapayPayload()
    {
        // Korapay extraction reads data.reference dynamically — requires ExpandoObject
        dynamic payload = new ExpandoObject();
        payload.reference = "KR_test_ref_001";
        payload.status    = "success";

        string? result = GatewayExtractor.ExtractReferenceFromWebhook(payload, PaymentGatewayType.Korapay);

        result.Should().Be("KR_test_ref_001");
    }

    [Fact]
    public void ExtractReferenceFromWebhook_ReturnsNull_WhenNoKnownPropertyExists()
    {
        var payload = new Dictionary<string, object>
        {
            ["completely_unknown"] = "value"
        };

        var result = GatewayExtractor.ExtractReferenceFromWebhook(payload, PaymentGatewayType.Automatic);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractReferenceFromWebhook_ReturnsNull_WhenExceptionThrown()
    {
        // Passing a non-dictionary object should catch and return null
        var result = GatewayExtractor.ExtractReferenceFromWebhook("bad-payload", PaymentGatewayType.Paystack);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("reference",             "REF_001")]
    [InlineData("transaction_reference", "REF_002")]
    [InlineData("txn_ref",              "REF_003")]
    [InlineData("id",                   "REF_004")]
    public void ExtractReferenceFromWebhook_FallsBackToCommonPropertyNames(
        string propertyName, string expectedRef)
    {
        var payload = new Dictionary<string, object>
        {
            [propertyName] = expectedRef
        };

        var result = GatewayExtractor.ExtractReferenceFromWebhook(payload, PaymentGatewayType.Automatic);

        result.Should().Be(expectedRef);
    }
}
