using PayBridge.SDK;
using PayBridge.SDK.Enums;
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
//builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Add DB repository based on the configured provider
builder.Services.AddDBRepository(builder.Configuration, "MSSQL");
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

builder.Services.AddPayBridge(config =>
{
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
    //app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers(); 
app.Run();
