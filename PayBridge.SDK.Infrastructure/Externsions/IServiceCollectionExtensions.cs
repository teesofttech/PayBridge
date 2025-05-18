using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayBridge.SDK.Domain.Enums;
using PayBridge.SDK.Domain;
using PayBridge.SDK.Infrastructure.Factories;
using PayBridge.SDK.Infrastructure.Gateways;
using PayBridge.SDK.Infrastructure.Persistence;
using PayBridge.SDK.Infrastructure.Repositories;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Application.Services;

namespace PayBridge.SDK.Infrastructure.Externsions;
public static class IServiceCollectionExtensions
{
    private const int RetryCount = 3;
    private const double RetryInSeconds = 1.1;

    public static IServiceCollection AddDBRepository(this IServiceCollection services, IConfiguration configuration, string provider)
    {
        var connectionString = configuration.GetConnectionString(nameof(PayBridgeDbContext));
        var providerConfigured = configuration["DatabaseProvider"] ?? provider;
        if (provider == providerConfigured)
        {
            services.AddDbContext<PayBridgeDbContext>(options =>
            {
                options.UseSqlServer(connectionString,
                opt =>
                opt.EnableRetryOnFailure(
                   RetryCount,
                    TimeSpan.FromSeconds(
                        RetryInSeconds),
                    null));
            }, ServiceLifetime.Scoped);
        }
        else if (provider == providerConfigured)
        {
            services.AddDbContext<PayBridgeDbContext>(options =>
            {
                options.UseNpgsql(connectionString,
                opt =>
                opt.EnableRetryOnFailure(
                   RetryCount,
                    TimeSpan.FromSeconds(
                        RetryInSeconds),
                    null));
            }, ServiceLifetime.Scoped);
        }
        else if (provider == providerConfigured)
        {
            //services.AddDbContext<PayBridgeDbContext>(options =>
            //{
            //    options.UseMySql(connectionString,
            //    opt =>
            //    opt.EnableRetryOnFailure(
            //       RetryCount,
            //        TimeSpan.FromSeconds(
            //            RetryInSeconds),
            //        null));
            //}, ServiceLifetime.Scoped);
        }
        else
        {
            throw new NotSupportedException($"The provider '{provider}' is not supported.");
        }

        return services;
    }

    public static IServiceCollection AddPayBridge(this IServiceCollection services, Action<PaymentGatewayConfig> configAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configAction == null)
        {
            throw new ArgumentNullException(nameof(configAction));
        }

        services.AddOptions<PaymentGatewayConfig>()
                       .Configure<IConfiguration>((settings, configuration) =>
                       {
                           configuration.GetSection("PaymentGatewayConfig").Bind(settings);
                       });


        // Configure the SDK
        var config = new PaymentGatewayConfig();
        configAction(config);
        services.AddSingleton(config);

        // Register core services
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<PaymentGatewayFactory>();

        // Register gateways
        RegisterGateways(services, config);

        return services;
    }

    private static void RegisterGateways(IServiceCollection services, PaymentGatewayConfig config)
    {
        // If no gateways are explicitly enabled, enable all available ones
        if (config.EnabledGateways.Count == 0)
        {
            services.AddScoped<StripeGateway>();
            services.AddScoped<PaystackGateway>();
            services.AddScoped<FlutterwaveGateway>();
            services.AddScoped<CheckoutGateway>();
            services.AddScoped<BenefitPayGateway>();
            services.AddScoped<KnetGateway>();
            return;
        }

        // Register only the enabled gateways
        foreach (var gateway in config.EnabledGateways)
        {
            switch (gateway)
            {
                case PaymentGatewayType.Stripe:
                    services.AddScoped<StripeGateway>();
                    break;
                case PaymentGatewayType.Paystack:
                    services.AddScoped<PaystackGateway>();
                    break;
                case PaymentGatewayType.Flutterwave:
                    services.AddScoped<FlutterwaveGateway>();
                    break;
                case PaymentGatewayType.Checkout:
                    services.AddScoped<CheckoutGateway>();
                    break;
                case PaymentGatewayType.BenefitPay:
                    services.AddScoped<BenefitPayGateway>();
                    break;
                case PaymentGatewayType.Knet:
                    services.AddScoped<KnetGateway>();
                    break;
            }
        }
    }

}
