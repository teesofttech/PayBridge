using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Dtos.Webhooks;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Services;

public sealed class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly PaymentGatewayConfig config;
    private readonly TimeProvider timeProvider;

    public WebhookSignatureVerifier(
        PaymentGatewayConfig config,
        TimeProvider timeProvider)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public WebhookVerificationResult Verify(WebhookVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RawBody);
        ArgumentNullException.ThrowIfNull(request.Headers);

        return request.Gateway switch
        {
            PaymentGatewayType.Paystack => VerifyHexHmac(
                request,
                "x-paystack-signature",
                config.Paystack.SecretKey,
                HashAlgorithmName.SHA512),
            PaymentGatewayType.Flutterwave => VerifyBase64Hmac(
                request,
                "flutterwave-signature",
                config.FlutterwaveConfig.WebhookSecretHash,
                HashAlgorithmName.SHA256),
            PaymentGatewayType.Stripe => VerifyStripe(request),
            PaymentGatewayType.Checkout => VerifyHexHmac(
                request,
                "Cko-Signature",
                config.Checkout.WebhookSecret,
                HashAlgorithmName.SHA256),
            PaymentGatewayType.Monnify => VerifyHexHmac(
                request,
                "monnify-signature",
                config.Monnify.SecretKey,
                HashAlgorithmName.SHA512),
            PaymentGatewayType.Squad => VerifyHexHmac(
                request,
                "x-squad-encrypted-body",
                config.Squad.SecretKey,
                HashAlgorithmName.SHA512),
            PaymentGatewayType.Korapay => VerifyKorapay(request),
            PaymentGatewayType.PeachPayments => VerifyPeachPayments(request),
            _ => WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.UnsupportedGateway)
        };
    }

    private WebhookVerificationResult VerifyStripe(WebhookVerificationRequest request)
    {
        if (!HasSecret(config.Stripe.WebhookSecret))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingConfiguration);
        }

        if (!TryGetHeader(request.Headers, "Stripe-Signature", out var signatureHeader))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingSignature);
        }

        var components = signatureHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(part => part.Length == 2)
            .ToList();

        var timestampText = components
            .FirstOrDefault(part => part[0].Equals("t", StringComparison.Ordinal))?
            .ElementAtOrDefault(1);
        var signatures = components
            .Where(part => part[0].Equals("v1", StringComparison.Ordinal))
            .Select(part => part[1])
            .ToList();

        if (!long.TryParse(timestampText, out var timestamp) || signatures.Count == 0)
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MalformedSignature);
        }

        if (!IsWithinTolerance(timestamp))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.TimestampOutsideTolerance);
        }

        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var payload = Combine(prefix, request.RawBody);
        var expected = ComputeHmac(
            HashAlgorithmName.SHA256,
            config.Stripe.WebhookSecret,
            payload);

        return signatures.Any(signature => FixedTimeEqualsHex(expected, signature))
            ? WebhookVerificationResult.Valid
            : WebhookVerificationResult.Invalid(WebhookVerificationFailure.InvalidSignature);
    }

    private WebhookVerificationResult VerifyKorapay(WebhookVerificationRequest request)
    {
        if (!HasSecret(config.Korapay.SecretKey))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingConfiguration);
        }

        if (!TryGetHeader(request.Headers, "x-korapay-signature", out var signature))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingSignature);
        }

        try
        {
            using var document = JsonDocument.Parse(request.RawBody);
            if (!document.RootElement.TryGetProperty("data", out var data))
            {
                return WebhookVerificationResult.Invalid(
                    WebhookVerificationFailure.InvalidPayload);
            }

            var expected = ComputeHmac(
                HashAlgorithmName.SHA256,
                config.Korapay.SecretKey,
                Encoding.UTF8.GetBytes(data.GetRawText()));

            return FixedTimeEqualsHex(expected, signature)
                ? WebhookVerificationResult.Valid
                : WebhookVerificationResult.Invalid(WebhookVerificationFailure.InvalidSignature);
        }
        catch (JsonException)
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.InvalidPayload);
        }
    }

    private WebhookVerificationResult VerifyPeachPayments(WebhookVerificationRequest request)
    {
        if (!HasSecret(config.PeachPayments.WebhookSecret))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingConfiguration);
        }

        if (!TryGetHeader(request.Headers, "x-webhook-signature", out var signature) ||
            !TryGetHeader(request.Headers, "x-webhook-timestamp", out var timestampText) ||
            !TryGetHeader(request.Headers, "x-webhook-id", out var webhookId) ||
            !TryGetHeader(request.Headers, "x-webhook-signature-algorithm", out var algorithm))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingSignature);
        }

        if (!algorithm.Equals("HMACSHA256", StringComparison.OrdinalIgnoreCase) &&
            !algorithm.Equals("HMAC-SHA256", StringComparison.OrdinalIgnoreCase))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MalformedSignature);
        }

        if (!long.TryParse(timestampText, out var timestamp))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MalformedSignature);
        }

        if (!IsWithinTolerance(timestamp))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.TimestampOutsideTolerance);
        }

        var prefix = Encoding.UTF8.GetBytes(
            $"{timestampText}.{webhookId}.{request.RequestUri}.");
        var message = Combine(prefix, request.RawBody);
        var expected = ComputeHmac(
            HashAlgorithmName.SHA256,
            config.PeachPayments.WebhookSecret,
            message);

        return FixedTimeEqualsHex(expected, signature)
            ? WebhookVerificationResult.Valid
            : WebhookVerificationResult.Invalid(WebhookVerificationFailure.InvalidSignature);
    }

    private static WebhookVerificationResult VerifyHexHmac(
        WebhookVerificationRequest request,
        string headerName,
        string secret,
        HashAlgorithmName algorithm)
    {
        if (!HasSecret(secret))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingConfiguration);
        }

        if (!TryGetHeader(request.Headers, headerName, out var signature))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingSignature);
        }

        var expected = ComputeHmac(algorithm, secret, request.RawBody);
        return FixedTimeEqualsHex(expected, signature)
            ? WebhookVerificationResult.Valid
            : WebhookVerificationResult.Invalid(WebhookVerificationFailure.InvalidSignature);
    }

    private static WebhookVerificationResult VerifyBase64Hmac(
        WebhookVerificationRequest request,
        string headerName,
        string secret,
        HashAlgorithmName algorithm)
    {
        if (!HasSecret(secret))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingConfiguration);
        }

        if (!TryGetHeader(request.Headers, headerName, out var signature))
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MissingSignature);
        }

        byte[] received;
        try
        {
            received = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return WebhookVerificationResult.Invalid(
                WebhookVerificationFailure.MalformedSignature);
        }

        var expected = ComputeHmac(algorithm, secret, request.RawBody);
        return CryptographicOperations.FixedTimeEquals(expected, received)
            ? WebhookVerificationResult.Valid
            : WebhookVerificationResult.Invalid(WebhookVerificationFailure.InvalidSignature);
    }

    private bool IsWithinTolerance(long timestamp)
    {
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var tolerance = Math.Max(1, config.WebhookTimestampToleranceSeconds);
        return timestamp >= now - tolerance && timestamp <= now + tolerance;
    }

    private static byte[] ComputeHmac(
        HashAlgorithmName algorithm,
        string secret,
        byte[] payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        return algorithm == HashAlgorithmName.SHA512
            ? HMACSHA512.HashData(key, payload)
            : HMACSHA256.HashData(key, payload);
    }

    private static bool FixedTimeEqualsHex(byte[] expected, string receivedHex)
    {
        byte[] received;
        try
        {
            received = Convert.FromHexString(receivedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    private static bool TryGetHeader(
        IReadOnlyDictionary<string, string> headers,
        string name,
        out string value)
    {
        foreach (var header in headers)
        {
            if (header.Key.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(header.Value))
            {
                value = header.Value.Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool HasSecret(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

    private static byte[] Combine(byte[] prefix, byte[] payload)
    {
        var combined = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, combined, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, combined, prefix.Length, payload.Length);
        return combined;
    }
}
