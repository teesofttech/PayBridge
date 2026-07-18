# Refund persistence and balance reservations

PayBridge records every provider refund attempt. A refund is inserted as
`Pending` before the provider call so concurrent requests reserve refundable
balance atomically. Provider responses update the same record to `Pending`,
`Refunded`, or `Failed`.

Only `Pending` and `Refunded` records consume refundable balance. Failed attempts
remain available for audit but do not prevent a replacement refund. A payment is
marked `Refunded` only when cumulative provider-confirmed refunds reach the
captured payment amount; an asynchronous pending response does not update the
payment state prematurely.

The audit record stores identifiers, amount, currency, reason, status, gateway,
timestamps, and the normalized `RefundResponse`. It does not store credentials,
authorization headers, or raw request payloads.

## Migration

Apply `AddRefundReservationIndex` with:

```bash
dotnet ef database update --project PayBridge.SDK/PayBridge.SDK.csproj
```

The migration adds an index over
`Refunds(PaymentTransactionReference, Status)`. SQL Server requires the indexed
reference to be bounded, so the column changes from `nvarchar(max)` to
`nvarchar(450)`.

Before applying it to an existing SQL Server database, confirm no stored value
exceeds that limit:

```sql
SELECT Id, LEN(PaymentTransactionReference) AS ReferenceLength
FROM Refunds
WHERE LEN(PaymentTransactionReference) > 450;
```

Resolve any returned rows before deployment. Back up the database using the
normal operational procedure before applying a production migration.

## Rollback

To roll back only this migration, target the previous migration:

```bash
dotnet ef database update 20250525015225_SetupDB \
  --project PayBridge.SDK/PayBridge.SDK.csproj
```

The down migration removes the composite index and restores
`PaymentTransactionReference` to `nvarchar(max)`. Refund audit rows are retained.
