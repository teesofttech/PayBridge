# Automatic gateway routing

Automatic routing considers only configured gateways in the currency's declared
compatibility list. A configured `DefaultGateway` is selected when it is both
registered and compatible; otherwise PayBridge uses the deterministic order for
that currency. DI registration order does not affect the result.

Automatic routing currently supports `PaymentMethodType.Card` only. For
`BankTransfer`, `MobileMoney`, `Wallet`, `Ussd`, and `QrCode`, specify a
concrete gateway explicitly in the payment request.

PayBridge rejects unknown currencies, unsupported payment methods, and cases
where no compatible gateway is configured before making a provider request. It
does not fall back to an arbitrary registered gateway.

Current currency groups are:

- NGN: Monnify, Squad, Korapay, Interswitch, Remita, OPay, Paystack, Flutterwave
- KES/GHS/UGX/TZS/ZAR/RWF/ZMW/CDF/XOF/XAF/MWK: Peach Payments, pawaPay, DPO,
  Flutterwave, Paystack
- BWP: Peach Payments, DPO
- BHD: BenefitPay
- KWD: KNET
- USD/EUR/GBP: Stripe, Checkout.com
- JPY: Stripe

Cryptocurrency is not implemented by any current PayBridge gateway and is
rejected explicitly. Provider capability metadata for finer payment-method and
regional routing remains tracked by issue #90.
