// ┌─────────────────────────────────────────────────────────────────────────┐
// │  PayBridge.SDK — Example Application                                    │
// │                                                                         │
// │  This file is the canonical "how to wire up PayBridge" reference.       │
// │  Read top-to-bottom; each numbered section explains one concept.        │
// └─────────────────────────────────────────────────────────────────────────┘

using PayBridge.SDK;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Example.Services;
using Serilog;
using System.Threading.RateLimiting;

// ── 1. LOGGING (optional — any ILogger provider works) ────────────────────
//
// We use Serilog here for pretty structured console output so you can see
// exactly what the SDK is doing on each request.

var log = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(log);

// ── 2. OPENAPI / SWAGGER ───────────────────────────────────────────────────
//
// Visit https://localhost:{port}/swagger after running to explore all
// endpoints interactively without needing Postman.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PayBridge.SDK — Example API",
        Version = "v1",
        Description = "Demonstrates every PayBridge.SDK integration pattern: " +
                      "explicit gateway selection, automatic routing, webhook " +
                      "processing, redirect-back verification, and refunds."
    });

    // Include XML doc comments so Swagger shows parameter descriptions.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

builder.Services
    .AddOptions<DemoUiSecurityOptions>()
    .Bind(builder.Configuration.GetSection(DemoUiSecurityOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<DemoUiSecurityOptions>, DemoUiSecurityOptionsValidator>();

// ── 3a. DATABASE (optional) ────────────────────────────────────────────────
//
// PayBridge can persist every transaction automatically.
// If you don't need persistence, comment the next two lines out — the rest
// of the example still works fine with the in-memory OrderService.
//
// Supported providers: "SqlServer" | "Npgsql" | "MySql"
// Connection string is read from appsettings.json → ConnectionStrings.PayBridgeDbContext

// builder.Services.AddDBRepository(builder.Configuration, "SqlServer");

// ── 3b. ADD PAYBRIGE SDK ───────────────────────────────────────────────────
//
// AddPayBridge reads "PaymentGatewayConfig" from appsettings.json and then
// lets you override / extend anything in the lambda.
//
// PATTERN A — Configuration driven (recommended for production):
//   Non-secret settings may live in appsettings.json. Credentials must come from
//   user-secrets locally or environment variables / a managed secret store in production.
//   The lambda is left empty (or used only for feature flags).
//
// PATTERN B — Fully in-code (tests only):
//   Supply fake credentials directly in isolated test setup, never in committed source.
//
// This example shows PATTERN A so you can see how the config file maps to
// the object model.

builder.Services.AddPayBridge(builder.Configuration, config =>
{
    // The SDK binds "PaymentGatewayConfig" from the complete configuration stack,
    // including appsettings, user-secrets, environment variables, and secret stores.

    // ── Override / extend specific settings in code ────────────────────────
    //
    // Any property you set here wins over the bound configuration.
    // Uncomment and edit the block that matches the gateway you want to test.

    // ── Explicitly choose which gateways to activate: ─────────────────────
    //
    // If EnabledGateways is empty the SDK enables ALL gateways whose config
    // section is present. Listing them explicitly is safer for production.
    //
    // The list is also read from "PaymentGatewayConfig:EnabledGateways" in
    // appsettings.json — you don't have to set it here.

    // config.EnabledGateways = new List<PaymentGatewayType>
    // {
    //     PaymentGatewayType.Paystack,
    //     PaymentGatewayType.Flutterwave,
    //     PaymentGatewayType.Stripe,
    // };

    // ── PATTERN B example (fully in-code, no appsettings.json keys) ───────
    //
    // config.Paystack = new() { SecretKey = "sk_test_...", PublicKey = "pk_test_..." };
    // config.FlutterwaveConfig = new() { SecretKey = "FLWSECK_TEST-...", PublicKey = "FLWPUBK_TEST-..." };
    // config.Stripe = new() { SecretKey = "sk_test_..." };
});

// ── 4. IN-MEMORY ORDER SERVICE ────────────────────────────────────────────
//
// A tiny in-memory store that simulates the "business logic" side of things:
// track pending orders and mark them as paid when a webhook/callback arrives.
// In a real app this would be your domain service / EF Core repository.

builder.Services.AddSingleton<PayBridge.SDK.Example.Services.OrderService>();
builder.Services.AddSingleton<PayBridge.SDK.Example.Services.IWebhookReplayStore,
    PayBridge.SDK.Example.Services.WebhookReplayStore>();

// ─────────────────────────────────────────────────────────────────────────────

var app = builder.Build();
var demoSecurity = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DemoUiSecurityOptions>>().Value;
var isHosted = !app.Environment.IsDevelopment();

// ── 5. MIDDLEWARE PIPELINE ─────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PayBridge Example v1");
        c.RoutePrefix = string.Empty; // Swagger at root "/"
    });
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    await next();
});

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var isProviderCallback =
        context.Request.Path.StartsWithSegments("/api/webhook") ||
        context.Request.Path.StartsWithSegments("/api/verify");

    if (isHosted && demoSecurity.RequireAuthenticatedAccess && !isProviderCallback)
    {
        var suppliedApiKey = context.Request.Headers["X-Demo-Api-Key"].ToString();
        if (!string.Equals(suppliedApiKey, demoSecurity.ApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }
    }

    if (isHosted && demoSecurity.RequireCsrfHeader && !isProviderCallback &&
        HttpMethods.IsPost(context.Request.Method))
    {
        var suppliedCsrf = context.Request.Headers[demoSecurity.CsrfHeaderName].ToString();
        if (!string.Equals(suppliedCsrf, demoSecurity.CsrfHeaderValue, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_csrf" });
            return;
        }
    }

    if (isHosted && demoSecurity.Mode == DemoRuntimeMode.Live && !demoSecurity.AllowLiveMode)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "live_mode_blocked" });
        return;
    }

    await next();
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
