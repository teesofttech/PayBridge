# Webhook replay protection

The example webhook verifies the provider signature before calculating a replay
receipt. The receipt is a SHA-256 hash of the authenticated provider and exact
raw request body. Only one concurrent delivery can begin processing a receipt.

Completed receipts are retained for 24 hours and the example store is capped at
10,000 entries. Failed verification or order matching abandons the receipt so a
provider retry can be processed again. Successful fulfillment retains it, and
`OrderService.MarkAsPaid` is independently idempotent.

`WebhookReplayStore` is intentionally an in-memory example. Production systems
must implement the same atomic `TryStart`, `Complete`, and `Abandon` contract in
a shared durable store so protection survives restarts and works across service
instances. Receipt retention should be at least as long as each provider's
documented webhook retry window.
