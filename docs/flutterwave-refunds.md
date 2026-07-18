# Flutterwave refunds

PayBridge supports full and partial Flutterwave refunds through Flutterwave's
v3 transaction refund API.

The SDK accepts the merchant transaction reference (`tx_ref`) used throughout
PayBridge. Before creating the refund, it queries Flutterwave's
`verify_by_reference` endpoint and uses the returned numeric `data.id` in:

```text
POST /v3/transactions/{id}/refund
```

The amount is sent in the currency's major unit, matching Flutterwave's payment
and refund APIs. PayBridge always sends an explicit amount:

- an amount equal to the verified transaction amount is a full refund;
- a smaller amount is a partial refund;
- an amount greater than the verified transaction is rejected before the refund
  request is sent.

Flutterwave may return a refund as `new`, `pending`, or `processing`; PayBridge
maps those accepted asynchronous states to `PaymentStatus.Pending`. Completed or
succeeded responses map to `PaymentStatus.Refunded`, while provider failures map
to `PaymentStatus.Failed`.

## Sandbox verification

The integration test is deliberately opt-in because a successful refund consumes
the refundable balance of its transaction. Configure only sandbox values:

```text
FLUTTERWAVE_SECRET_KEY
FLUTTERWAVE_REFUND_TRANSACTION_REFERENCE
FLUTTERWAVE_REFUND_AMOUNT
```

`FLUTTERWAVE_REFUND_TRANSACTION_REFERENCE` must identify a dedicated successful
sandbox payment. Replace the fixture after it has been fully refunded. Never use
live credentials or a customer transaction.

Run the test with:

```bash
dotnet test PayBridge.SDK.Test/PayBridge.SDK.Test.csproj \
  --filter FullyQualifiedName~FlutterwaveRefundIntegrationTests
```

Official references:

- [Flutterwave refunds guide](https://developer.flutterwave.com/v3.0/docs/refunds)
- [Flutterwave transaction refund API](https://developer.flutterwave.com/v3.0/reference/transaction-refund)
