# PayBridge.SDK

<h1 align="center">PayBridge.SDK</h1>

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
  <a href="https://github.com/teesofttech/PayBridge/blob/master/LICENSE">
    <img src="https://img.shields.io/github/license/teesofttech/PayBridge" alt="License">
  </a>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#getting-started">Getting Started</a> •
  <a href="#usage">Usage</a> •
  <a href="#contributing">Contributing</a> •
  <a href="#license">License</a>
</p>

## Features

- **Unified API**: Interact with multiple payment gateways (e.g., Flutterwave, Paystack, Fincra, Stripe) through a single, consistent interface.
- **Database Flexibility**: Choose between using PayBridge's default database or integrating with your own.
- **Transaction Logging**: Automatically records transaction details for auditing and reporting.
- **Extensible Architecture**: Built with Clean Architecture principles, facilitating easy maintenance and scalability.

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Database system (e.g., SQL Server, PostgreSQL)

### Installation

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/teesofttech/PayBridge.git
   cd PayBridge

2. **Set Up Database**:

    Using Default Database: The SDK is configured to use a default database. Ensure your connection string is set in appsettings.json:
    ```bash
    "ConnectionStrings": {
      "DefaultConnection": "Your_Default_Connection_String"
    }
3. **Apply Migrations**:
      ```bash
      dotnet ef database update
4. **Build and Run**:
     ```bash
     dotnet build
     dotnet run

  <h2>🔧 Usage</h2>
  
  <h3>📁 Configuring Payment Gateways</h3>
  
  <p>To configure your payment gateways, add the following section to your <code>appsettings.json</code> file. Replace the placeholder keys with your actual credentials from each provider:</p>
  
  <pre>
  <code>
  "PaymentGateways": {
    "Flutterwave": {
      "ApiKey": "Your_Flutterwave_Api_Key"
    },
    "Paystack": {
      "ApiKey": "Your_Paystack_Api_Key"
    },
    "Fincra": {
      "ApiKey": "Your_Fincra_Api_Key"
    },
    "Stripe": {
      "ApiKey": "Your_Stripe_Api_Key"
    }
  }
  </code>
  </pre>
  
  <p>This allows PayBridge to dynamically route requests to the appropriate gateway using your configured credentials.</p>
  
  <p>✅ Make sure your <code>Startup.cs</code> or <code>Program.cs</code> correctly loads configuration from <code>appsettings.json</code>.</p>

  <h3>💳 Processing a Payment</h3>

<p>To initiate a payment through a specific gateway, inject the <code>IPaymentGatewayFactory</code> and call the corresponding service.</p>

<pre>
<code>
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Application.Models;

public class PaymentService
{
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public PaymentService(IPaymentGatewayFactory paymentGatewayFactory)
    {
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task&lt;PaymentResponse&gt; ProcessPayment(PaymentRequest request, string gatewayName)
    {
        var gateway = _paymentGatewayFactory.Create(gatewayName);
        var response = await gateway.ProcessPaymentAsync(request);

        // You can optionally log this transaction
        return response;
    }
}
</code>
</pre>

<p>📌 <strong>Note:</strong> Replace <code>gatewayName</code> with values like <code>"Flutterwave"</code>, <code>"Paystack"</code>, etc.</p>


<h3>🔔 Handling Webhooks</h3>

<p>Most payment gateways send callbacks to notify your application of transaction updates. Use the provided <code>IWebhookHandler</code> to process incoming events:</p>

<pre>
<code>
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
    public async Task&lt;IActionResult&gt; HandleWebhook(string gatewayName)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var result = await _webhookHandler.HandleAsync(gatewayName, json);

        if (result.Success)
            return Ok();

        return BadRequest(result.ErrorMessage);
    }
}
</code>
</pre>

<p>⚠️ Make sure the route <code>/api/webhook/{gatewayName}</code> matches the URL set in your payment gateway dashboard.</p>

<h3>🤝 Contributing</h3>

<p>We welcome your ideas, improvements, and fixes! To contribute:</p>

<ol>
  <li><strong>Fork</strong> the repository → <a href="https://github.com/teesofttech/PayBridge">PayBridge on GitHub</a></li>
  <li><strong>Clone</strong> your fork:
    <pre><code>git clone https://github.com/your-username/PayBridge.git</code></pre>
  </li>
  <li><strong>Create a feature branch</strong>:
    <pre><code>git checkout -b feature/your-feature-name</code></pre>
  </li>
  <li><strong>Implement</strong> your feature or fix</li>
  <li><strong>Commit</strong> your changes:
    <pre><code>git commit -m "feat: add new feature"</code></pre>
  </li>
  <li><strong>Push</strong> to your fork:
    <pre><code>git push origin feature/your-feature-name</code></pre>
  </li>
  <li><strong>Create a Pull Request</strong> and describe your change</li>
</ol>

<p>🙌 Your support helps us make PayBridge better for everyone!</p>


<h3>📄 License</h3>

<p>PayBridge is released under the <a href="https://github.com/teesofttech/PayBridge/blob/main/LICENSE">MIT License</a>.</p>
<p>You’re free to use, modify, and distribute this software as long as you include the original license.</p>


    

  
  

    


   


