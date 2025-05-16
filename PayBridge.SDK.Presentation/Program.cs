using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Application.Services;
using PayBridge.SDK.Domain.Enums;
using PayBridge.SDK.Infrastructure.Externsions;

var builder = WebApplication.CreateBuilder(args);

IConfiguration Configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDBRepository(builder.Configuration, "MSSQL");

builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPaymentService, PaymentService>();
//builder.Services.AddScoped<IPaymentGateway, FlutterwaveGateway>();
//builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddPayBridge(config =>
{
    config.ApiKey = Configuration["PayBridge:ApiKey"];
    config.SecretKey = Configuration["PayBridge:SecretKey"];
    config.EnabledGateways = new List<PaymentGatewayType>
    {
        PaymentGatewayType.Stripe,
        PaymentGatewayType.Paystack
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

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
