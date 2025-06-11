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
  <a href="#-sample-payloads--responses">Sample Payloads & Responses</a> •
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
"DatabaseProvider": "MSSQL",
  "ConnectionStrings": {
    "PayBridgeDbContext": "Data Source=BABATUNDE;Initial Catalog=PayBridgeDB;Integrated Security=True;TrustServerCertificate=true;"
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

### 💳 Sample Payment Controller

```csharp
[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Creating payment for {Amount} {Currency}", request.Amount, request.Currency);

            var paymentRequest = PaymentRequestMapper.MapToPaymentRequest(request);
            var paymentGateway = request.Gateway ?? PaymentGatewayType.Automatic;

            var response = await _paymentService.CreatePaymentAsync(paymentRequest, paymentGateway);

            if (response.Success)
            {
                _logger.LogInformation("Payment created successfully: {Reference}", response.TransactionReference);
                return Ok(response);
            }
            else
            {
                _logger.LogWarning("Payment creation failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse { Message = response.Message, ErrorCode = "PAYMENT_FAILED" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment");
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing your payment",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    [HttpGet("verify-transaction")]
    public async Task<IActionResult> VerifyTransaction()
    {
        try
        {
            var queryParams = HttpContext.Request.Query;
            var allParams = new Dictionary<string, string>();
            foreach (var param in queryParams)
            {
                if (param.Key.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
                    param.Key.Contains("tx_ref", StringComparison.OrdinalIgnoreCase))
                {
                    allParams["reference"] = param.Value!;
                }
            }

            PaymentGatewayType gateway = GatewayExtractor.DetectGatewayFromWebhook(allParams.Values);
            string reference = allParams.Values.FirstOrDefault()!.ToString()!;

            if (string.IsNullOrEmpty(reference))
            {
                _logger.LogWarning("Could not extract transaction reference from Query");
                return BadRequest(new ErrorResponse
                {
                    Message = "Could not extract transaction reference from Query",
                    ErrorCode = "INVALID_REQUEST"
                });
            }

            var response = await _paymentService.VerifyPaymentAsync(reference, gateway);

            if (response.Success)
            {
                _logger.LogInformation("Query verification successful: {Reference}, Status: {Status}",
                    reference, response.Status);
                return Ok(new { success = true, response });
            }
            else
            {
                _logger.LogWarning("Query verification failed: {Message}", response.Message);
                return BadRequest(new ErrorResponse { Message = response.Message, ErrorCode = "QUERY_VERIFICATION_FAILED" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Query");
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing the Query",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }
}
```

---

## 📦 Sample Payloads & Responses

### 🔼 Sample Payment Request Payload

```json
{
  "amount": 1000,
  "currency": "NGN",
  "description": "est",
  "customerEmail": "tunde@yopmail.com",
  "customerName": "string",
  "customerPhone": "string",
  "redirectUrl": "https://localhost:7252/api/payment/verify-transaction",
  "webhookUrl": "https://localhost:7252/api/payment/verify-transaction",
  "metadata": {
    "additionalProp1": "string",
    "additionalProp2": "string",
    "additionalProp3": "string"
  },
  "paymentMethodType": 0,
  "savedPaymentMethodId": "string",
  "gateway": 1
}
```

### 🔽 Sample Payment Response

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

## 🤝 Contributing

We welcome your ideas, improvements, and fixes!

1. **Fork** the repo → [PayBridge on GitHub](https://github.com/teesofttech/PayBridge)
2. **Clone** your fork:

```bash
git clone https://github.com/teesofttech/PayBridge.git
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
