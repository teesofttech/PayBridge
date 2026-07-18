# Payment creation idempotency

Set `PaymentRequest.IdempotencyKey` to a stable, client-generated value for each
logical checkout attempt. UUID v4 values are recommended. Keys may contain up to
255 characters.

PayBridge reserves the key before calling a gateway and stores the normalized
`PaymentResponse`. Repeating the same key and payment parameters returns that
stored response without another gateway call. Reusing a key with different
amount, currency, customer, redirect, webhook, payment method, or gateway data
is rejected.

If a gateway call fails without a definitive response, the reservation remains
pending because the provider outcome may be uncertain. Retry using the same key;
do not generate a new key for a network timeout. Pending attempts require
provider reconciliation before a new provider request is safe.

Requests without an idempotency key retain the legacy behavior and are not safe
to retry automatically.

Provider-native behavior differs. Stripe supports an `Idempotency-Key` header;
Paystack enforces unique transaction references. PayBridge's persisted key is
the provider-neutral protection and does not replace provider reconciliation.
