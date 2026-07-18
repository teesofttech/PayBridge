using PayBridge.SDK.Constants;
using PayBridge.SDK.Enums;
using System.Text.Json;

namespace PayBridge.SDK.Helper;

public static class GatewayExtractor
{

    /// <summary>
    /// Detects which payment gateway sent the webhook notification
    /// based on the structure of the webhook data
    /// </summary>
    public static PaymentGatewayType DetectGatewayFromWebhook(object webhookData)
    {
        var result = DetectGatewayFromWebhookResult(webhookData);
        return result.Status == WebhookGatewayDetectionStatus.Detected
            ? result.Gateway!.Value
            : PaymentGatewayType.Automatic;
    }

    /// <summary>
    /// Detects a provider from an unauthenticated payload shape. Prefer the
    /// authenticated provider supplied by the webhook route whenever available.
    /// </summary>
    public static WebhookGatewayDetectionResult DetectGatewayFromWebhookResult(
        object? webhookData)
    {
        if (webhookData is null)
        {
            return WebhookGatewayDetectionResult.Invalid;
        }

        try
        {
            var payload = webhookData is JsonElement element
                ? element
                : JsonSerializer.SerializeToElement(webhookData);
            if (payload.ValueKind != JsonValueKind.Object)
            {
                return WebhookGatewayDetectionResult.Invalid;
            }

            var candidates = new HashSet<PaymentGatewayType>();
            var data = GetObject(payload, "data");

            if (HasString(payload, "flw_ref") ||
                (data is { } flutterwaveData && HasString(flutterwaveData, "flw_ref")) ||
                (StartsWith(payload, "id", "wbk_") && HasProperty(payload, "timestamp")))
            {
                candidates.Add(PaymentGatewayType.Flutterwave);
            }

            if (!candidates.Contains(PaymentGatewayType.Flutterwave) &&
                HasString(payload, "event") && data is not null)
            {
                candidates.Add(PaymentGatewayType.Paystack);
            }

            if (StartsWith(payload, "id", "evt_") &&
                HasString(payload, "type") &&
                data is { } stripeData &&
                stripeData.TryGetProperty("object", out _))
            {
                candidates.Add(PaymentGatewayType.Stripe);
            }

            if (payload.TryGetProperty("_links", out var links) &&
                links.ValueKind == JsonValueKind.Object)
            {
                candidates.Add(PaymentGatewayType.Checkout);
            }

            var reference = GetString(payload, "reference") ??
                (data is { } providerData ? GetString(providerData, "reference") : null);
            if (reference?.StartsWith(
                    GatewayReferencePrefixes.Korapay,
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                candidates.Add(PaymentGatewayType.Korapay);
            }

            return candidates.Count switch
            {
                0 => WebhookGatewayDetectionResult.Unknown,
                1 => WebhookGatewayDetectionResult.Detected(candidates.Single()),
                _ => WebhookGatewayDetectionResult.Ambiguous
            };
        }
        catch (Exception)
        {
            return WebhookGatewayDetectionResult.Invalid;
        }
    }

    /// <summary>
    /// Extracts the transaction reference from the webhook notification
    /// based on the gateway that sent it
    /// </summary>
    public static string? ExtractReferenceFromWebhook(object webhookData, PaymentGatewayType gateway)
    {
        // Convert to dynamic to inspect properties
        dynamic data = webhookData;

        try
        {
            switch (gateway)
            {
                case PaymentGatewayType.Paystack:
                    // Paystack webhook data is nested in a 'data' property
                    return data.data.reference;

                case PaymentGatewayType.Flutterwave:
                    // Flutterwave uses 'tx_ref' for the merchant reference
                    return data.tx_ref;

                case PaymentGatewayType.Stripe:
                    // Stripe's webhook data depends on the event type
                    if (data.type.ToString().StartsWith("payment_intent."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    // For charge events
                    else if (data.type.ToString().StartsWith("charge."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    break;

                case PaymentGatewayType.Checkout:
                    // Checkout.com webhook format
                    return data.data.reference;

                case PaymentGatewayType.Korapay:
                    return data.reference;
            }

            // If we couldn't extract a reference using the known formats,
            // try some common property names
            var dict = (IDictionary<string, object>)data;

            foreach (var prop in new[] { "reference", "transaction_reference", "txn_ref", "id" })
            {
                if (dict.ContainsKey(prop))
                {
                    return dict[prop].ToString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a transaction reference from an authenticated raw JSON webhook.
    /// </summary>
    public static string? ExtractReferenceFromWebhook(JsonElement webhookData, PaymentGatewayType gateway)
    {
        return gateway switch
        {
            PaymentGatewayType.Paystack => GetString(webhookData, "data", "reference"),
            PaymentGatewayType.Flutterwave =>
                GetString(webhookData, "data", "reference") ??
                GetString(webhookData, "data", "tx_ref") ??
                GetString(webhookData, "tx_ref"),
            PaymentGatewayType.Stripe =>
                GetString(webhookData, "data", "object", "metadata", "reference") ??
                GetString(webhookData, "data", "object", "client_reference_id"),
            PaymentGatewayType.Checkout => GetString(webhookData, "data", "reference"),
            PaymentGatewayType.Monnify =>
                GetString(webhookData, "eventData", "paymentReference"),
            PaymentGatewayType.Squad =>
                GetString(webhookData, "transaction_ref") ??
                GetString(webhookData, "transaction_reference") ??
                GetString(webhookData, "TransactionRef") ??
                GetString(webhookData, "Body", "transaction_ref"),
            PaymentGatewayType.Korapay =>
                GetString(webhookData, "data", "reference") ??
                GetString(webhookData, "reference"),
            PaymentGatewayType.PeachPayments =>
                GetString(webhookData, "merchantTransactionId") ??
                GetString(webhookData, "merchant_transaction_id"),
            _ => null
        };
    }

    /// <summary>
    /// Extracts a transaction reference from authenticated form fields.
    /// </summary>
    public static string? ExtractReferenceFromWebhook(
        IReadOnlyDictionary<string, string> fields,
        PaymentGatewayType gateway)
    {
        if (gateway != PaymentGatewayType.PeachPayments)
        {
            return null;
        }

        return GetValue(fields, "merchantTransactionId") ??
            GetValue(fields, "merchant_transaction_id");
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string> fields,
        string name)
    {
        foreach (var field in fields)
        {
            if (field.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();
    }

    private static JsonElement? GetObject(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static bool HasString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static bool StartsWith(JsonElement element, string name, string prefix) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        value.GetString()?.StartsWith(prefix, StringComparison.Ordinal) == true;

}

public enum WebhookGatewayDetectionStatus
{
    Detected,
    Unknown,
    Ambiguous,
    Invalid
}

public sealed record WebhookGatewayDetectionResult(
    WebhookGatewayDetectionStatus Status,
    PaymentGatewayType? Gateway)
{
    public static WebhookGatewayDetectionResult Unknown { get; } =
        new(WebhookGatewayDetectionStatus.Unknown, null);
    public static WebhookGatewayDetectionResult Ambiguous { get; } =
        new(WebhookGatewayDetectionStatus.Ambiguous, null);
    public static WebhookGatewayDetectionResult Invalid { get; } =
        new(WebhookGatewayDetectionStatus.Invalid, null);

    public static WebhookGatewayDetectionResult Detected(PaymentGatewayType gateway) =>
        new(WebhookGatewayDetectionStatus.Detected, gateway);
}
