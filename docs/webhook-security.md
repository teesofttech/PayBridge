# Webhook security

Webhook bodies are untrusted input. PayBridge verifies a provider-specific
signature over the original request bytes before parsing JSON, extracting a
transaction reference, calling a provider API, or fulfilling an order.

Use an explicit provider route:

```text
POST /api/webhook/{gateway}
```

For example, configure Paystack to call `/api/webhook/Paystack` and Stripe to
call `/api/webhook/Stripe`. Body-shape detection is not an authentication
mechanism and is not used by the secure endpoint.

## Implemented signature contracts

| Provider | Header | Verification | Replay protection |
|---|---|---|---|
| Paystack | `x-paystack-signature` | HMAC-SHA512 of raw body with API secret | Provider does not sign a timestamp; application idempotency is required |
| Flutterwave | `flutterwave-signature` | Base64 HMAC-SHA256 of raw body with dashboard webhook secret | Provider event ID plus application idempotency |
| Stripe | `Stripe-Signature` | HMAC-SHA256 of `timestamp.rawBody` with endpoint secret | Signed timestamp, five-minute default tolerance |
| Checkout.com | `Cko-Signature` | HMAC-SHA256 of raw body with workflow signing key | Provider event ID plus application idempotency |
| Monnify | `monnify-signature` | HMAC-SHA512 of raw body with client secret | Provider reference plus application idempotency |
| Squad | `x-squad-encrypted-body` | HMAC-SHA512 of raw body with secret key | Provider reference plus application idempotency |
| Korapay | `x-korapay-signature` | HMAC-SHA256 of the raw `data` object with secret key | Provider reference plus application idempotency |
| Peach Payments | `x-webhook-*` headers | HMAC-SHA256 of `timestamp.id.url.rawBody` with webhook secret | Signed timestamp and webhook ID, five-minute default tolerance |

Peach Payments sends the initial configuration event as JSON and regular
Checkout events as `application/x-www-form-urlencoded`; both formats are parsed
only after the same raw-body signature succeeds.

Flutterwave's current webhook contract uses the `flutterwave-signature` HMAC
header. Older Flutterwave integrations may use the legacy `verif-hash` shared
secret header; that legacy contract is not accepted by this verifier.

Signature comparisons use constant-time byte comparison. Missing configuration,
missing or malformed headers, unknown providers, and invalid signatures fail
closed with HTTP 401. Full bodies, signatures, and credentials are never logged.

Set webhook-only secrets with .NET user-secrets during local development:

```bash
dotnet user-secrets set "PaymentGatewayConfig:Stripe:WebhookSecret" "<endpoint-secret>" \
  --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
dotnet user-secrets set "PaymentGatewayConfig:FlutterwaveConfig:WebhookSecretHash" "<webhook-secret>" \
  --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
dotnet user-secrets set "PaymentGatewayConfig:Checkout:WebhookSecret" "<workflow-signing-key>" \
  --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
dotnet user-secrets set "PaymentGatewayConfig:PeachPayments:WebhookSecret" "<webhook-secret>" \
  --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
```

Set `PaymentGatewayConfig:WebhookTimestampToleranceSeconds` to control the
Stripe and Peach replay window. The default is 300 seconds. Keep server clocks
synchronized; do not set an unbounded tolerance.

Peach includes the request URL in its signed message. When the application is
behind a reverse proxy, configure ASP.NET Core forwarded-header middleware so
`Request.GetDisplayUrl()` reconstructs the public HTTPS URL that Peach called.

## Providers without an implemented signature contract

BenefitPay, KNET, Interswitch, Remita, OPay, DPO Group, and pawaPay currently
fail closed at the shared webhook verifier. Do not expose the generic webhook
endpoint for these providers. Use their server-to-server transaction status API
before changing payment or order state.

pawaPay supports optional RFC 9421 public-key signed callbacks. That protocol
requires public-key retrieval and rotation, content-digest validation, covered
request-component reconstruction, and expiry enforcement. It must be implemented
as a dedicated verifier rather than approximated with the API token.

Signature verification authenticates delivery; it does not prove the payment
amount, currency, reference, or final state. Always call the provider status API
and compare those values with the original order. Processing must also be
idempotent because providers retry webhook deliveries.

## Provider documentation

- [Paystack webhooks](https://paystack.com/docs/payments/webhooks/)
- [Flutterwave webhooks](https://developer.flutterwave.com/docs/webhooks)
- [Stripe webhook signatures](https://docs.stripe.com/webhooks/signature)
- [Checkout.com webhook verification](https://support.checkout.com/hc/en-us/articles/29686673313426-Verify-a-webhook-was-sent-by-Checkout-com)
- [Monnify webhooks](https://developers.monnify.com/docs/webhooks)
- [Squad signature validation](https://docs.squadco.com/webhook-direct-url/signature-validation/)
- [Korapay webhooks](https://developers.korapay.com/docs/webhooks)
- [Peach Payments Checkout webhooks](https://developer.peachpayments.com/docs/checkout-webhooks)
- [pawaPay callback signatures](https://docs.pawapay.io/using_the_api)
