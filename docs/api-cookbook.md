# API Cookbook

This cookbook gives practical, end-to-end integration patterns with production-oriented checklists.

## Flow 1: Create Payment -> Redirect -> Verify -> Fulfill

## Request (Create)

```json
{
  "amount": 5000,
  "currency": "NGN",
  "description": "Order ORD-1042",
  "customerEmail": "jane@example.com",
  "customerName": "Jane Doe",
  "customerPhone": "+2348012345678",
  "redirectUrl": "https://merchant.example.com/pay/callback",
  "webhookUrl": "https://merchant.example.com/api/webhook/Paystack",
  "paymentMethodType": 0,
  "gateway": 2
}
```

## Server workflow

1. Call CreatePaymentAsync with explicit gateway when possible.
2. Persist order as Pending with transaction reference.
3. Redirect user to checkoutUrl from SDK response.
4. On redirect or webhook, call VerifyPaymentAsync.
5. Fulfill only when provider verification confirms success and expected amount/currency.

## Production checklist

- Never fulfill based only on redirect query or webhook payload body.
- Compare verified amount/currency/reference against original order.
- Use idempotency keys for create operations exposed to client retries.

## Flow 2: Automatic Routing with Fallback

## Goal

Route by currency while preserving deterministic behavior and safe fallback.

## Pattern

1. Keep gateway config explicit and validated on startup.
2. Set DefaultGateway only when it is compatible and configured.
3. Reject unsupported combinations before external provider calls.

## Production checklist

- Monitor selected gateway and routing reason in structured logs.
- Keep per-provider callback URLs aligned with actual selected provider.
- Validate startup configuration in CI/CD before deployment.

## Flow 3: Secure Webhooks

## Pattern

1. Preserve raw request body bytes.
2. Verify provider signature before JSON parsing.
3. Reject missing/invalid signatures with no side effects.
4. Apply replay protection and idempotent processing.
5. Re-verify transaction with provider API before final state update.

## Production checklist

- Do not log full webhook payloads, signatures, or secrets.
- Keep webhook timestamp tolerance tight and clocks synchronized.
- Store replay receipt IDs with bounded retention.

## Flow 4: Refund Request and Reconciliation

## Pattern

1. Accept refund request with explicit amount and reason.
2. Call RefundPaymentAsync.
3. Persist refund attempt with provider status and reference.
4. Reconcile pending refunds asynchronously until terminal state.

## Production checklist

- Treat refund as asynchronous by default.
- Guard against duplicate refund submissions with idempotency keys.
- Reconcile uncertain provider outcomes using durable persistence jobs.

## Error-Handling Playbook

- 4xx request errors: map to actionable client messages.
- Timeout/transient network failures: retry only where idempotent.
- Provider failures: normalize status and preserve provider reference IDs for audit.
- Security failures: fail closed and return minimal error detail.
