# PayBridge.SDK

**Seamlessly integrate multiple payment gateways into your ASP.NET Core applications with a single, unified API.**

[![NuGet](https://img.shields.io/nuget/v/PayBridge.SDK)](https://www.nuget.org/packages/PayBridge.SDK)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PayBridge.SDK)](https://www.nuget.org/packages/PayBridge.SDK)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/teesofttech/PayBridge/blob/main/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-teesofttech%2FPayBridge-blue)](https://github.com/teesofttech/PayBridge)

---

## What is PayBridge?

PayBridge.SDK is an open-source .NET 8 library that provides a **single, unified interface** to 14+ payment gateways across Nigeria, Africa, and globally. Stop writing gateway-specific code — define your payment logic once and switch providers with a single configuration change.

---

## Supported Gateways

| Gateway | Region | Currency | Auth Method |
|---|---|---|---|
| **Flutterwave** | Global / Africa | NGN, USD, GBP, EUR, … | Secret Key |
| **Paystack** | Nigeria / Africa | NGN, GHS, ZAR | Secret Key |
| **Stripe** | Global | USD, GBP, EUR, … | Secret Key |
| **Checkout.com** | Global | Multi-currency | Secret + Public Key |
| **Monnify** | Nigeria | NGN | OAuth2 ******
| **Squad (GTCo)** | Nigeria | NGN | ******
| **Korapay** | Nigeria / Africa | NGN | ******
| **Interswitch / Quickteller** | Nigeria | NGN | OAuth2 + HMAC-SHA512 |
| **Remita** | Nigeria | NGN | SHA512 Hash |
| **OPay** | Nigeria / Africa | NGN | HMAC-SHA512 |
| **DPO Group** | Africa (19+ countries) | KES, GHS, UGX, ZAR, USD | XML API v6 |
| **PawaPay** | Africa (mobile money) | GHS, TZS, UGX, RWF, ZMW, … | REST ******
| **BenefitPay** | Bahrain / GCC | BHD | Merchant API |
| **Knet** | Kuwait | KWD | Transport Key |

---

## Features

- ✅ **Unified API** — one interface for all gateways: create, verify, and refund payments
- ✅ **Smart Routing** — `Automatic` mode picks the best gateway based on currency
- ✅ **Transaction Logging** — built-in persistence for auditing and reporting
- ✅ **Multi-database Support** — SQL Server, PostgreSQL, MySQL, SQLite
- ✅ **Clean Architecture** — repository pattern, DI-friendly, extensible
- ✅ **Refund Support** — full create/verify/refund lifecycle for all gateways
- ✅ **Sandbox / Production** — per-gateway environment toggle

---

## Installation

```bash
dotnet add package PayBridge.SDK
```

Or via the NuGet Package Manager Console:

```powershell
Install-Package PayBridge.SDK
```

---

## Quick Start

### 1. Register PayBridge in `Program.cs`

```csharp
using PayBridge.SDK;
using PayBridge.SDK.Enums;

var builder = WebApplication.CreateBuilder(args);

// Register the database (SQL Server | Npgsql | MySql)
builder.Services.AddDBRepository(builder.Configuration, "MSSQL");

// Register PayBridge with your gateway credentials
builder.Services.AddPayBridge(config =>
{
    config.EnabledGateways = new List<PaymentGatewayType>
    {
        PaymentGatewayType.Flutterwave,
        PaymentGatewayType.Paystack,
        PaymentGatewayType.Stripe
    };

    config.FlutterwaveConfig.PublicKey    = "FLWPUBK-xxxx";
    config.FlutterwaveConfig.SecretKey    = "FLWSECK-xxxx";
    config.FlutterwaveConfig.EncryptionKey = "xxxx";

    config.Paystack.PublicKey = "pk_test_xxxx";
    config.Paystack.SecretKey = "sk_test_xxxx";

    config.Stripe.SecretKey = "sk_test_xxxx";
});

var app = builder.Build();
app.Run();
```

### 2. Configure `appsettings.json`

```json
{
  "DatabaseProvider": "MSSQL",
  "ConnectionStrings": {
    "PayBridgeDbContext": "Server=localhost;Database=PayBridgeDB;Trusted_Connection=True;TrustServerCertificate=true;"
  },
  "PaymentGatewayConfig": {
    "EnabledGateways": [ 1, 2, 3 ],
    "FlutterwaveConfig": {
      "PublicKey": "FLWPUBK-xxxx",
      "SecretKey": "FLWSECK-xxxx",
      "EncryptionKey": "xxxx"
    },
    "Paystack": {
      "PublicKey": "pk_test_xxxx",
      "SecretKey": "sk_test_xxxx"
    },
    "Stripe": {
      "SecretKey": "sk_test_xxxx"
    }
  }
}
```

### 3. Apply Migrations

```bash
dotnet ef database update
```

---

## Usage

### Inject `IPaymentService`

```csharp
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }
}
```

---

### Create a Payment

```csharp
var request = new PaymentRequest
{
    Amount          = 5000,
    Currency        = "NGN",
    Description     = "Order #1234",
    CustomerEmail   = "customer@example.com",
    CustomerName    = "John Doe",
    CustomerPhone   = "+2348012345678",
    RedirectUrl     = "https://yourdomain.com/payment/callback",
    WebhookUrl      = "https://yourdomain.com/api/payment/webhook",
    PaymentMethodType = PaymentMethodType.Card,
    Metadata        = new Dictionary<string, string>
    {
        { "orderId", "1234" },
        { "userId", "user_abc" }
    }
};

// Use a specific gateway
PaymentResponse response = await _paymentService.CreatePaymentAsync(request, PaymentGatewayType.Flutterwave);

// Or let PayBridge auto-select based on currency
PaymentResponse response = await _paymentService.CreatePaymentAsync(request, PaymentGatewayType.Automatic);

if (response.Success)
{
    // Redirect customer to the checkout URL
    return Redirect(response.CheckoutUrl);
}
```

**Sample Response:**

```json
{
  "success": true,
  "transactionReference": "FLW_dc324e96d52b4bd48c401ff9194c15e8",
  "message": "Hosted Link",
  "checkoutUrl": "https://checkout-v2.dev-flutterwave.com/v3/hosted/pay/207b10ab0a05ddf19746",
  "status": 0,
  "gatewayResponse": {
    "link": "https://checkout-v2.dev-flutterwave.com/v3/hosted/pay/207b10ab0a05ddf19746"
  }
}
```

---

### Verify a Payment

```csharp
// Pass the reference returned by the gateway (from redirect/webhook)
VerificationResponse verification = await _paymentService.VerifyPaymentAsync(
    transactionReference: "FLW_dc324e96d52b4bd48c401ff9194c15e8",
    gateway: PaymentGatewayType.Flutterwave  // or Automatic
);

if (verification.Success && verification.Status == PaymentStatus.Successful)
{
    // Fulfill the order
}
```

**Sample Verification Response:**

```json
{
  "success": true,
  "transactionReference": "FLW_dc324e96d52b4bd48c401ff9194c15e8",
  "message": "Successful",
  "amount": 5000.00,
  "currency": "NGN",
  "status": 1,
  "paymentDate": "2025-06-01T10:30:00Z",
  "fee": 75.00,
  "paymentMethod": "card",
  "amountSettled": 4925.00,
  "metadata": {},
  "gatewayResponse": {}
}
```

---

### Process a Refund

```csharp
var refundRequest = new RefundRequest
{
    TransactionReference = "FLW_dc324e96d52b4bd48c401ff9194c15e8",
    Amount = 5000,
    Reason = "Customer requested refund"
};

RefundResponse refund = await _paymentService.RefundPaymentAsync(refundRequest);

if (refund.Success)
{
    // Refund processed
    Console.WriteLine($"Refund reference: {refund.RefundReference}");
}
```

---

## All Gateway Configuration Examples

### Nigerian Gateways

```json
"PaymentGatewayConfig": {
  "Monnify": {
    "ApiKey": "MK_TEST_xxxx",
    "SecretKey": "xxxx",
    "ContractCode": "xxxx"
  },
  "Squad": {
    "SecretKey": "sandbox_sk_xxxx",
    "PublicKey": "sandbox_pk_xxxx",
    "IsSandbox": true
  },
  "Korapay": {
    "PublicKey": "pk_sandbox_xxxx",
    "SecretKey": "sk_sandbox_xxxx"
  },
  "Interswitch": {
    "ClientId": "xxxx",
    "ClientSecret": "xxxx",
    "MerchantCode": "xxxx",
    "PaymentItemCode": "xxxx",
    "IsSandbox": true
  },
  "Remita": {
    "MerchantId": "xxxx",
    "ServiceTypeId": "xxxx",
    "ApiKey": "xxxx",
    "IsSandbox": true
  },
  "Opay": {
    "MerchantId": "xxxx",
    "PublicKey": "xxxx",
    "SecretKey": "xxxx",
    "IsSandbox": true
  }
}
```

### Pan-African Gateways

```json
"PaymentGatewayConfig": {
  "DpoGroup": {
    "CompanyToken": "xxxx",
    "PaymentCurrency": "KES",
    "IsSandbox": true
  },
  "PawaPay": {
    "ApiToken": "xxxx",
    "IsSandbox": true
  }
}
```

### GCC / Middle East Gateways

```json
"PaymentGatewayConfig": {
  "BenefitPay": {
    "MerchantId": "xxxx",
    "ApiKey": "xxxx",
    "ApiSecret": "xxxx"
  },
  "Knet": {
    "TransportId": "xxxx",
    "Password": "xxxx",
    "TerminalResourceKey": "xxxx"
  }
}
```

---

## Database Providers

PayBridge supports **SQL Server**, **PostgreSQL**, **MySQL**, and **SQLite** via Entity Framework Core.

```csharp
// SQL Server
builder.Services.AddDBRepository(builder.Configuration, "MSSQL");

// PostgreSQL
builder.Services.AddDBRepository(builder.Configuration, "Npgsql");

// MySQL
builder.Services.AddDBRepository(builder.Configuration, "MySql");
```

Accepted `DatabaseProvider` values in `appsettings.json`:

| Value | Provider |
|---|---|
| `MSSQL` / `SqlServer` | SQL Server |
| `Npgsql` / `PostgreSQL` / `Postgres` | PostgreSQL |
| `MySql` | MySQL |

---

## Models Reference

### `PaymentRequest`

| Property | Type | Description |
|---|---|---|
| `Amount` | `decimal` | Payment amount |
| `Currency` | `string` | ISO currency code (e.g. `NGN`, `USD`) |
| `Description` | `string` | Payment description |
| `CustomerEmail` | `string` | Customer's email address |
| `CustomerName` | `string` | Customer's full name |
| `CustomerPhone` | `string` | Customer's phone number |
| `RedirectUrl` | `string` | URL to redirect after payment |
| `WebhookUrl` | `string` | Webhook URL for async notifications |
| `PaymentMethodType` | `PaymentMethodType` | `Card`, `BankTransfer`, `MobileMoney`, `Wallet`, `Ussd`, `QrCode` |
| `Metadata` | `Dictionary<string,string>` | Custom key/value pairs |
| `SavedPaymentMethodId` | `string` | ID of a saved payment method |

### `PaymentResponse`

| Property | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` if payment was initiated successfully |
| `TransactionReference` | `string` | Unique transaction reference |
| `Message` | `string` | Gateway message |
| `CheckoutUrl` | `string` | URL to redirect the customer to |
| `Status` | `PaymentStatus` | `Pending`, `Successful`, `Failed`, `Cancelled`, `Refunded` |
| `GatewayResponse` | `Dictionary<string,string>` | Raw gateway response fields |

### `VerificationResponse`

| Property | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` if verification succeeded |
| `TransactionReference` | `string` | Transaction reference |
| `Amount` | `decimal` | Amount charged |
| `Currency` | `string` | Currency code |
| `Status` | `PaymentStatus` | Payment status |
| `PaymentDate` | `DateTime` | When the payment was made |
| `Fee` | `decimal` | Gateway fee deducted |
| `AmountSettled` | `decimal?` | Net amount settled |
| `PaymentMethod` | `string` | Payment method used |

### `RefundRequest`

| Property | Type | Description |
|---|---|---|
| `TransactionReference` | `string` | Reference of the payment to refund |
| `Amount` | `decimal` | Amount to refund |
| `Reason` | `string` | Reason for the refund |

### `PaymentGatewayType` Enum

| Value | Gateway |
|---|---|
| `0` | Automatic |
| `1` | Flutterwave |
| `2` | Paystack |
| `3` | Stripe |
| `4` | Checkout.com |
| `5` | BenefitPay |
| `6` | Knet |
| `7` | Monnify |
| `8` | Squad |
| `9` | Korapay |
| `10` | Interswitch |
| `11` | Remita |
| `12` | OPay |
| `13` | DPO Group |
| `14` | PawaPay |

---

## What's New in v1.1.0

- 🆕 **Monnify** — OAuth2, NGN, bank transfer & USSD
- 🆕 **Squad by GTCo** — Static Bearer, NGN, cards & transfers
- 🆕 **Korapay** — Bearer, NGN, virtual accounts & cards
- 🆕 **Interswitch / Quickteller** — OAuth2 + HMAC-SHA512, NGN
- 🆕 **Remita** — SHA512 hash auth, government & corporate payments
- 🆕 **OPay** — HMAC-SHA512 signed, mobile money & cards
- 🆕 **DPO Group** — XML API v6, 19+ African countries
- 🆕 **PawaPay** — REST Bearer, African mobile money
- ⚡ Smart currency routing: NGN → Nigerian gateways, African currencies → DPO/PawaPay
- ⚡ Transaction reference prefix detection per gateway

---

## Requirements

- .NET 8.0+
- EF Core-supported database (SQL Server, PostgreSQL, MySQL, SQLite)

---

## Contributing

Contributions are welcome! Please read our [Contributing Guide](https://github.com/teesofttech/PayBridge/blob/main/CONTRIBUTING.md) and open a Pull Request.

---

## License

PayBridge.SDK is licensed under the [MIT License](https://github.com/teesofttech/PayBridge/blob/main/LICENSE).

---

## Links

- 📦 [NuGet Package](https://www.nuget.org/packages/PayBridge.SDK)
- 🐙 [GitHub Repository](https://github.com/teesofttech/PayBridge)
- 🐛 [Report an Issue](https://github.com/teesofttech/PayBridge/issues)
- 👤 Author: **Babatunde Esanju** — Teesoftech
