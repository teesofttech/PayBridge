# Gateway Capability Matrix

This matrix documents implemented SDK capabilities at a feature-contract level.

Notes:

- Status reflects SDK adapter support, not merchant-account enablement.
- Provider dashboards and account permissions can still block runtime operations.
- For webhook handling, only providers with implemented signature contracts should use public webhook endpoints.

| Gateway | Create | Verify | Refund Adapter | Webhook Signature Contract | Auth Pattern | Primary Currencies |
|---|---|---|---|---|---|---|
| Paystack | Yes | Yes | Yes | Yes | Bearer secret key + HMAC-SHA512 webhook signature | NGN, GHS, ZAR, USD, KES |
| Flutterwave | Yes | Yes | Yes | Yes | Bearer secret key + Base64 HMAC webhook signature | NGN, GHS, KES, ZAR, UGX, USD, EUR, GBP |
| Stripe | Yes | Yes | Yes | Yes | Bearer secret key + signed timestamp webhook | USD, EUR, GBP, AUD, CAD, JPY |
| Checkout.com | Yes | Yes | Yes | Yes | Bearer secret key + webhook signature | USD, EUR, GBP, AED |
| BenefitPay | Yes | Yes | Yes | No | Merchant API key/secret | BHD |
| Knet | Yes | Yes | Yes | No | Transport credentials | KWD |
| Monnify | Yes | Yes | Yes | Yes | OAuth2-style secret flow + webhook signature | NGN |
| Squad | Yes | Yes | Yes | Yes | Bearer secret key + webhook signature | NGN |
| Korapay | Yes | Yes | Yes | Yes | Bearer secret key + data-field signature | NGN, GHS, KES |
| Interswitch | Yes | Yes | Yes | No | OAuth2 + HMAC request signing | NGN |
| Remita | Yes | Yes | Yes | No | Hash-based API credentials | NGN |
| OPay | Yes | Yes | Yes | No | HMAC request signing | NGN |
| DPO Group | Yes | Yes | Yes | No | XML API token model | KES, GHS, UGX, ZAR, USD |
| PawaPay | Yes | Yes | Yes | No (dedicated signature protocol not yet implemented) | Bearer token | GHS, TZS, UGX, RWF, ZMW |
| PeachPayments | Yes | Yes | Yes | Yes | Bearer AccessToken + signed webhook tuple | ZAR, KES, NGN, BWP, USD |

## Operational Guidance

## Use explicit provider webhooks when signature contracts exist

- Configure callback paths as /api/webhook/{gateway} and map each provider dashboard to its provider-specific route.

## Do not expose generic public webhooks for unsupported signature-contract providers

- Providers without implemented signature verification should rely on server-to-server verification workflows before state change.

## Refund behavior in production

- Adapter support means the SDK has a refund path.
- Provider-side final status can still be Pending or Failed based on asynchronous processing, entitlement, and transaction state.
