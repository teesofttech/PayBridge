﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK;
public static class IServiceCollectionExtensions
{
    private const int RetryCount = 3;
    private const double RetryInSeconds = 1.1;

    /// <summary>
    /// Add DB repository to the service collection based on the specified provider.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
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
            services.AddDbContext<PayBridgeDbContext>(options =>
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                opt =>
                opt.EnableRetryOnFailure(
                   RetryCount,
                    TimeSpan.FromSeconds(
                        RetryInSeconds),
                    null));
            }, ServiceLifetime.Scoped);
        }
        else
        {
            throw new NotSupportedException($"The provider '{provider}' is not supported.");
        }

        return services;
    }

    /// <summary>
    /// Add PayBridge SDK services to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configAction"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
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
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentGateway, FlutterwaveGateway>();
        services.AddScoped<IPaymentGateway, PaystackGateway>();
        services.AddHttpClient();

        // Register gateways
        RegisterGateways(services, config);

        return services;
    }

    /// <summary>
    /// Register payment gateways based on the configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
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
