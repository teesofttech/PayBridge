using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Application.Services;
using PayBridge.SDK.Domain.Enums;
using PayBridge.SDK.Infrastructure.Externsions;
using PayBridge.SDK.Infrastructure.Gateways;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

IConfiguration Configuration = builder.Configuration;

// Add services to the container.
var logger = new LoggerConfiguration().WriteTo.Console()
  .ReadFrom.Configuration(builder.Configuration)
  .Enrich.FromLogContext()
  .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDBRepository(builder.Configuration, "MSSQL");
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentGateway, FlutterwaveGateway>();
builder.Services.AddScoped<IPaymentGateway, PaystackGateway>();
//builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddPayBridge(config =>
{
   // config.ApiKey = Configuration["PayBridge:ApiKey"];
    //config.SecretKey = Configuration["PayBridge:SecretKey"];
    config.EnabledGateways = new List<PaymentGatewayType>
    {
        PaymentGatewayType.Stripe,
        PaymentGatewayType.Paystack,
        PaymentGatewayType.Flutterwave
    };
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers(); 
app.Run();
