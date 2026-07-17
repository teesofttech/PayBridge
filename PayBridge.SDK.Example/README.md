# PayBridge.SDK — Example Project

> **A complete, runnable reference application** that shows every integration
> pattern provided by [PayBridge.SDK](../README.md) — from wiring up the DI
> container to processing webhooks and issuing refunds.

---

## Table of Contents

1. [Prerequisites](#-prerequisites)
2. [5-Minute Quick Start](#-5-minute-quick-start)
3. [Project Structure](#-project-structure)
4. [Scenarios](#-scenarios)
   - [Scenario 1 — Explicit gateway](#scenario-1--explicit-gateway)
   - [Scenario 2 — Automatic gateway](#scenario-2--automatic-gateway)
   - [Scenario 3 — Multiple gateways](#scenario-3--multiple-gateways)
   - [Scenario 4 — Webhooks](#scenario-4--webhooks)
   - [Scenario 5 — Redirect-back verification](#scenario-5--redirect-back-verification)
   - [Scenario 6 — Refunds](#scenario-6--refunds)
5. [Configuration Reference](#-configuration-reference)
6. [Webhook Setup (local dev)](#-webhook-setup-local-dev)
7. [Common Errors & Fixes](#-common-errors--fixes)

---

## ✅ Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 8.0+ |
| A sandbox API key | At least one gateway (see links below) |
| Database (optional) | SQL Server / PostgreSQL / MySQL |

No database is required to run the example — `OrderService` is in-memory.

---

## ⚡ 5-Minute Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/teesofttech/PayBridge.git
cd PayBridge

# 2. Store sandbox credentials outside the repository
cd PayBridge.SDK.Example
dotnet user-secrets set "PaymentGatewayConfig:EnabledGateways:0" "Paystack"
dotnet user-secrets set "PaymentGatewayConfig:Paystack:PublicKey" "<sandbox-public-key>"
dotnet user-secrets set "PaymentGatewayConfig:Paystack:SecretKey" "<sandbox-secret-key>"

# 3. Run the example
dotnet run

# 4. Open Swagger in your browser
#    → http://localhost:5xxx  (port shown in terminal)
```

Swagger loads at the root URL `/`. Try **POST /api/payment** first.

---

## 📁 Project Structure

```
PayBridge.SDK.Example/
│
├── Program.cs                   ← DI registration — read this first
├── appsettings.json             ← Credential-free schema and placeholders
├── appsettings.Development.json ← Credential-free development settings
│
├── Controllers/
│   ├── PaymentController.cs     ← Scenario 1 & 2
│   ├── MultiGatewayController.cs← Scenario 3
│   ├── WebhookController.cs     ← Scenario 4
│   ├── VerifyController.cs      ← Scenario 5
│   ├── RefundController.cs      ← Scenario 6
│   └── OrdersController.cs      ← Debug helper
│
├── Models/
│   ├── CheckoutRequest.cs       ← Request body for payment creation
│   ├── RefundModel.cs           ← Request body for refunds
│   └── ApiResponse.cs           ← Generic { success, data, error } wrapper
│
├── Mapper/
│   └── CheckoutRequestMapper.cs ← Maps CheckoutRequest → SDK PaymentRequest
│
└── Services/
    └── OrderService.cs          ← In-memory order store (replace with EF Core)
```

---

## 🎬 Scenarios

### Scenario 1 — Explicit gateway

**When to use:** you know exactly which gateway the customer should pay through
(e.g. you only operate in Nigeria and always use Paystack).

**Endpoint:** `POST /api/payment`

```json
{
  "amount": 5000,
  "currency": "NGN",
  "description": "Order #1042 — Pro Subscription",
  "customerEmail": "jane@example.com",
  "customerName": "Jane Doe",
  "customerPhone": "+2348012345678",
  "redirectUrl": "http://localhost:5xxx/api/verify",
  "webhookUrl": "https://your-ngrok-url.ngrok.io/api/webhook/Paystack",
  "metadata": { "orderId": "ORD-1042" },
  "gateway": 2
}
```

> **Gateway values:** `0` = Automatic, `1` = Flutterwave, `2` = Paystack,
> `3` = Stripe, `9` = Korapay … see `PaymentGatewayType` enum for all values.

**Response:**
```json
{
  "success": true,
  "data": {
    "orderId": "A1B2C3D4E5F6",
    "transactionReference": "TXN_paystack_abc123",
    "checkoutUrl": "https://checkout.paystack.com/...",
    "gateway": "Paystack"
  }
}
```

Redirect your customer to `checkoutUrl`.

---

### Scenario 2 — Automatic gateway

**When to use:** you support multiple currencies / regions and want the SDK to
pick the best available gateway based on the currency.

**Endpoint:** `POST /api/payment` — omit `gateway` or set it to `0`.

```json
{
  "amount": 1999,
  "currency": "USD",
  "description": "Pro plan — monthly",
  "customerEmail": "jane@example.com",
  "customerName": "Jane Doe",
  "redirectUrl": "http://localhost:5xxx/api/verify",
  "webhookUrl": "https://your-ngrok-url.ngrok.io/api/webhook/Flutterwave"
}
```

The SDK will route USD to Stripe if it is enabled, or fall back to the
`DefaultGateway` configured in `appsettings.json`.

---

### Scenario 3 — Multiple gateways

**When to use:** you operate across multiple countries and need different
gateways per currency, or you want to offer customers a gateway choice.

**Endpoints:**

| Method | URL | Purpose |
|--------|-----|---------|
| `GET` | `/api/gateways` | List all active gateways with metadata |
| `GET` | `/api/gateways/{id}` | Inspect a single gateway's config status |

```jsonc
// GET /api/gateways — sample response
{
  "success": true,
  "data": {
    "count": 3,
    "gateways": [
      { "id": 2, "name": "Paystack",    "currencies": ["NGN","GHS","ZAR","USD","KES"] },
      { "id": 1, "name": "Flutterwave", "currencies": ["NGN","GHS","KES","ZAR","USD","EUR","GBP"] },
      { "id": 3, "name": "Stripe",      "currencies": ["USD","EUR","GBP","AUD","CAD"] }
    ],
    "tip": "Pass the 'id' value as the 'gateway' field in POST /api/payment..."
  }
}
```

**To enable more gateways** edit `appsettings.json` → `EnabledGateways` and add
the corresponding key block (e.g. `"Monnify": { ... }`).

---

### Scenario 4 — Webhooks

**When to use:** always. Webhooks are the authoritative source of payment status.
Never fulfil an order based only on the redirect-back URL.

**Endpoint:** `POST /api/webhook/{gateway}`

The controller:
1. Receives the raw gateway POST body.
2. Verifies the provider signature before parsing the JSON body.
3. Rejects missing, invalid, stale, and unsupported signatures without side effects.
4. Calls `GatewayExtractor.ExtractReferenceFromWebhook()` after authentication.
5. Calls `IPaymentService.VerifyPaymentAsync()` to **confirm** the status server-side.
6. Updates the in-memory order store.

**Testing locally** — see [Webhook Setup (local dev)](#-webhook-setup-local-dev).

---

### Scenario 5 — Redirect-back verification

**When to use:** the customer returns to your site after checkout. This is a
UX convenience — show them a "payment successful" page immediately.

**Endpoint:** `GET /api/verify`

The gateway appends the transaction reference as a query parameter:

| Gateway | Query parameter |
|---------|----------------|
| Paystack | `?reference=TXN_xxx` |
| Flutterwave | `?tx_ref=TXN_xxx` |
| Monnify | `?paymentReference=MNFY_xxx` |
| Stripe Checkout | `?session_id=cs_xxx` |

The controller normalises all variants, verifies the transaction, and returns:

```json
{
  "success": true,
  "data": {
    "transactionReference": "TXN_paystack_abc123",
    "status": "Successful",
    "amount": 5000,
    "currency": "NGN",
    "paymentDate": "2026-06-12T10:30:00Z",
    "orderId": "A1B2C3D4E5F6"
  }
}
```

---

### Scenario 6 — Refunds

**When to use:** customer returns, duplicate charge, or fulfilment failure.

**Endpoint:** `POST /api/refund`

```json
{
  "transactionReference": "TXN_paystack_abc123",
  "amount": 5000,
  "reason": "Customer requested cancellation within 24 h"
}
```

For a **partial refund** (₦2,500 of a ₦5,000 charge):

```json
{
  "transactionReference": "TXN_paystack_abc123",
  "amount": 2500,
  "reason": "Partial service not delivered"
}
```

Gateways with refund support: **Paystack ✓ | Flutterwave ✓ | Stripe ✓ | Korapay ✓ | PeachPayments ✓**

---

## ⚙️ Configuration Reference

Gateway configuration is bound under `PaymentGatewayConfig`. Tracked
`appsettings*.json` files contain only placeholders; use .NET user-secrets for
local credentials and environment variables or a managed secret store when deployed.

| Field | Type | Description |
|-------|------|-------------|
| `DefaultGateway` | string | Gateway used when `Automatic` cannot infer from currency |
| `EnabledGateways` | string[] | Only these gateways are registered in DI |
| `Paystack.SecretKey` | string | From Paystack dashboard → Settings → API Keys |
| `Paystack.PublicKey` | string | From Paystack dashboard |
| `FlutterwaveConfig.SecretKey` | string | From Flutterwave dashboard → Settings → API |
| `Stripe.SecretKey` | string | From Stripe dashboard → Developers → API keys |
| `Monnify.ApiKey` | string | From Monnify dashboard → Settings → API Keys |
| `Monnify.ContractCode` | string | From Monnify dashboard → Settings → Merchant |
| `Squad.SecretKey` | string | From Squad dashboard |
| `Squad.IsSandbox` | bool | `true` for test, `false` for live |
| `PeachPayments.EntityId` | string | From Peach Payments dashboard |
| `PeachPayments.AccessToken` | string | Bearer token from Peach Payments |
| `PeachPayments.IsSandbox` | bool | `true` for test |
| *(all others)* | — | Same pattern — see `appsettings.json` for the full list |

### Key sources

| Gateway | Dashboard URL |
|---------|--------------|
| Paystack | https://dashboard.paystack.com/#/settings/developers |
| Flutterwave | https://developer.flutterwave.com |
| Stripe | https://dashboard.stripe.com/apikeys |
| Korapay | https://merchant.korapay.com/dashboard/settings/api-integration |
| Monnify | https://app.monnify.com/developer/settings |
| Squad | https://dashboard.squadco.com |
| PeachPayments | https://dashboard.peachpayments.com |

---

## 🌐 Webhook Setup (local dev)

Gateways cannot POST to `localhost`. Expose your local port with a tunnel:

### ngrok (recommended)

```bash
# Install: https://ngrok.com/download
ngrok http 5xxx   # replace 5xxx with your actual port

# ngrok prints a public URL, e.g.:
# Forwarding  https://abcd1234.ngrok.io → http://localhost:5xxx

# Use that URL as webhookUrl:
# "webhookUrl": "https://abcd1234.ngrok.io/api/webhook/Paystack"
```

### localtunnel (alternative)

```bash
npm install -g localtunnel
lt --port 5xxx
```

---

## 🛠 Common Errors & Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| `Gateway 'X' is not registered in DI` | Gateway not enabled or configured | Set `EnabledGateways` and its credentials through user-secrets or environment variables |
| `SecretKey is required` | Config key is empty / placeholder | Set the key with `dotnet user-secrets` or an environment variable |
| `Could not find a transaction reference` | Wrong query parameter key on redirect | Check which key your gateway uses (see Scenario 5 table) |
| `Refund not supported` | Gateway does not have a refund API | Check supported gateways list in Scenario 6 |
| Webhook never arrives | Tunnel not running | Start ngrok and re-supply the URL when creating a payment |
| Build error: `CS0122 inaccessible` | Using an internal SDK exception type | Catch `InvalidOperationException` or the base `Exception` |

---

## 📄 License

MIT — same as the main PayBridge.SDK library.
