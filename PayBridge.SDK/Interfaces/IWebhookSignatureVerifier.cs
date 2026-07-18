using PayBridge.SDK.Dtos.Webhooks;

namespace PayBridge.SDK.Interfaces;

public interface IWebhookSignatureVerifier
{
    WebhookVerificationResult Verify(WebhookVerificationRequest request);
}
