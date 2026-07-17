using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Webhooks;

public sealed record WebhookVerificationRequest(
    PaymentGatewayType Gateway,
    byte[] RawBody,
    IReadOnlyDictionary<string, string> Headers,
    string Method,
    string RequestUri);

public sealed record WebhookVerificationResult(
    bool IsValid,
    WebhookVerificationFailure FailureReason)
{
    public static WebhookVerificationResult Valid { get; } =
        new(true, WebhookVerificationFailure.None);

    public static WebhookVerificationResult Invalid(WebhookVerificationFailure reason) =>
        new(false, reason);
}

public enum WebhookVerificationFailure
{
    None = 0,
    MissingConfiguration,
    MissingSignature,
    MalformedSignature,
    InvalidSignature,
    TimestampOutsideTolerance,
    InvalidPayload,
    UnsupportedGateway
}
