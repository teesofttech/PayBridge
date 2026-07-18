# Quick Start

This guide takes you from a clean repository checkout to a secure first payment flow.

## 1. Install and Restore

```bash
dotnet restore PayBridge.SDK.sln
dotnet build PayBridge.SDK.sln
```

## 2. Configure Database Provider

Use one supported provider and supply the connection string from secure sources.

Supported provider values:

- SqlServer (or MSSQL)
- Npgsql (or PostgreSQL/Postgres)
- MySql

## 3. Configure Gateway Credentials

Use .NET user-secrets locally and environment variables or managed secret stores in hosted environments.

Example:

```bash
dotnet user-secrets set "PaymentGatewayConfig:EnabledGateways:0" "Paystack" --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
dotnet user-secrets set "PaymentGatewayConfig:Paystack:SecretKey" "<sandbox-secret>" --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
```

## 4. Register Services

In your startup wiring:

- Register persistence with AddDBRepository
- Register SDK services with AddPayBridge

## 5. Run Example API

```bash
dotnet run --project PayBridge.SDK.Example/PayBridge.SDK.Example.csproj
```

## 6. Process Payment Lifecycle

1. Create payment using an explicit or automatic gateway route.
2. Redirect customer to checkout URL.
3. Verify callback/webhook reference through provider verification.
4. Persist payment status and fulfill order after successful verification.

## 7. Harden Before Production

- Enforce webhook signature verification and replay protection.
- Require authenticated access for hosted demo/API surfaces.
- Keep all credentials server-side.
- Ensure integration tests run against real sandbox credentials.
