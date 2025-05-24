# PayBridge.SDK

<p align="center">
  <strong>Seamlessly integrate multiple payment gateways into your ASP.NET Core applications.</strong>
</p>

<p align="center">
  <a href="https://github.com/teesofttech/PayBridge/releases">
    <img src="https://img.shields.io/github/v/release/teesofttech/PayBridge" alt="Latest Release">
  </a>
  <a href="https://github.com/teesofttech/PayBridge/issues">
    <img src="https://img.shields.io/github/issues/teesofttech/PayBridge" alt="Issues">
  </a>
  <a href="https://github.com/teesofttech/PayBridge/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/teesofttech/PayBridge" alt="License">
  </a>
</p>

<p align="center">
  <a href="#-features">Features</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-usage">Usage</a> •
  <a href="#-contributing">Contributing</a> •
  <a href="#-license">License</a>
</p>

---

## 🚀 Features

- **Unified API**: Interact with multiple payment gateways (e.g., Flutterwave, Paystack, Fincra, Stripe) through a single, consistent interface.
- **Database Flexibility**: Choose between PayBridge's default database or integrate with your own.
- **Transaction Logging**: Automatically records transaction details for auditing and reporting.
- **Extensible Architecture**: Built with Clean Architecture principles for maintainability and scalability.

---

## 🧰 Getting Started

### ✅ Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server or another supported database

### 📦 Installation

```bash
git clone https://github.com/teesofttech/PayBridge.git
cd PayBridge
```

### ⚙️ Configure the Database

Edit `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Your_Default_Connection_String"
}
```

### 📚 Apply Migrations

```bash
dotnet ef database update
```

### ▶️ Build and Run

```bash
dotnet build
dotnet run
```

---

## 💡 Usage

### 🧾 Configuring Payment Gateways

Update `appsettings.json`:

```json
{  
  "AllowedHosts": "*",
  "DatabaseProvider": "MSSQL",
  "ConnectionStrings": {
    "PayBridgeDbContext": "Data Source=BABATUNDE;Initial Catalog=PayBridgeDB;Integrated Security=True;TrustServerCertificate=true;"
  },
  "PaymentGatewayConfig": {
    "DefaultGateway": "Flutterwave",
    "EnabledGateways": [ "Paystack", "Flutterwave", "Stripe" ],
    "Paystack": {
      "PublicKey": "pk_test_xxxx",
      "SecretKey": "sk_test_58021058577ab054bfc8175053b96fec8b88585a"
    },
    "FlutterwaveConfig": {
      "PublicKey": "FLWPUBK_TEST-1e0d20b98a0a662d68abd35648c10ec3-X",
      "SecretKey": "FLWSECK_TEST-ad4dffd9eb56d6c5b05fd394c8b99245-X",
      "EncryptionKey": "FLWENCK_TEST-xxxx"
    },
    "Stripe": {
      "SecretKey": "sk_test_51Hxxx",
      "ApiVersion": "2023-10-16"
    }
  }
}

```

> ✅ Ensure `Program.cs` loads configuration correctly from `appsettings.json`.

### 💳 Processing a Payment

```csharp
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Application.Models;

public class PaymentService
{
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public PaymentService(IPaymentGatewayFactory paymentGatewayFactory)
    {
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<PaymentResponse> ProcessPayment(PaymentRequest request, string gatewayName)
    {
        var gateway = _paymentGatewayFactory.Create(gatewayName);
        return await gateway.ProcessPaymentAsync(request);
    }
}
```

> 📌 Use `gatewayName` like `"Flutterwave"`, `"Paystack"`, etc.

### 🔔 Handling Webhooks

```csharp
using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookHandler _webhookHandler;

    public WebhookController(IWebhookHandler webhookHandler)
    {
        _webhookHandler = webhookHandler;
    }

    [HttpPost("{gatewayName}")]
    public async Task<IActionResult> HandleWebhook(string gatewayName)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var result = await _webhookHandler.HandleAsync(gatewayName, json);

        return result.Success ? Ok() : BadRequest(result.ErrorMessage);
    }
}
```

> ⚠️ Ensure `/api/webhook/{gatewayName}` matches your gateway dashboard callback URL.

---

## 🤝 Contributing

We welcome your ideas, improvements, and fixes!

### Steps

1. **Fork** the repo → [PayBridge on GitHub](https://github.com/teesofttech/PayBridge)
2. **Clone** your fork:

   ```bash
   git clone https://github.com/your-username/PayBridge.git
   ```

3. **Create a feature branch**:

   ```bash
   git checkout -b feature/your-feature-name
   ```

4. **Implement**, **Commit**, and **Push**:

   ```bash
   git commit -m "feat: add new feature"
   git push origin feature/your-feature-name
   ```

5. **Create a Pull Request** and describe your changes.

🙌 Your support helps make PayBridge better for everyone!

---

## 📄 License

PayBridge is released under the [MIT License](https://github.com/teesofttech/PayBridge/blob/main/LICENSE).

> You're free to use, modify, and distribute this software as long as the original license is included.
