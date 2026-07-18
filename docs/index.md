# PayBridge Documentation

<div class="hero">
  <div class="hero__eyebrow">Production-grade Payment Orchestration for .NET</div>
  <h1>Build once. Route safely. Operate confidently.</h1>
  <p>
    PayBridge gives your team one integration surface for multi-gateway payments,
    verification, refunds, webhook security, and operational safeguards.
  </p>
  <a class="hero__cta" href="quickstart/">Start with Quick Start</a>
</div>

## What You Get

- Unified gateway abstraction for payment creation, verification, and refund operations.
- Secure webhook path with signature verification, replay protection, and fail-closed behavior.
- Deterministic automatic routing with clear configuration validation at startup.
- Testable architecture with unit and integration workflows aligned to release quality.

## Documentation Paths

### Start Here

- [Quick Start](quickstart.md): Configure, run, and process your first transaction flow.
- [Automatic Routing](automatic-gateway-routing.md): Understand route selection and fallback behavior.
- [Payment Idempotency](payment-idempotency.md): Prevent duplicate effects across retries.

### Security Hardening

- [Webhook Security](webhook-security.md): Signature validation contracts and provider-specific expectations.
- [Webhook Replay Protection](webhook-replay-protection.md): Replay detection strategy and operations notes.
- [Dependency Security](dependency-security.md): Vulnerability management and CI safeguards.
- [Credential Rotation](credential-rotation-and-history-purge.md): Incident response and secret hygiene.

### Reliability and Operations

- [Integration Testing](integration-testing.md): Sandbox preflight gates and run interpretation.
- [Refund Persistence](refund-persistence.md): Durable refund lifecycle handling.
- [Flutterwave Refunds](flutterwave-refunds.md): Known behavior and current implementation notes.

## Recommended Reading Order

1. Quick Start
2. Automatic Routing
3. Webhook Security
4. Integration Testing
5. Idempotency and Refund Persistence

## Release Readiness Checklist

- Gateway credentials configured through secure runtime providers.
- Webhook signatures verified before payload parsing or fulfillment.
- Idempotency and replay controls active for mutation endpoints.
- Unit and integration test gates passing in CI.
- Documentation and package claims aligned with implementation.
