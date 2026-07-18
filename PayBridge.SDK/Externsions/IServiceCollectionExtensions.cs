using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using PayBridge.SDK.Services;

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
        var providerConfigured = NormalizeProviderName(configuration["DatabaseProvider"] ?? provider);
        if (providerConfigured == "SqlServer")
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
        else if (providerConfigured == "Npgsql")
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
        else if (providerConfigured == "MySql")
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
            throw new NotSupportedException($"The provider '{providerConfigured}' is not supported. Use 'SqlServer', 'Npgsql', or 'MySql'.");
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
        if (configAction == null)
        {
            throw new ArgumentNullException(nameof(configAction));
        }

        var configuration = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IConfiguration))
            ?.ImplementationInstance as IConfiguration;

        return AddPayBridgeCore(services, configuration, configAction);
    }

    /// <summary>
    /// Add PayBridge SDK services and bind PaymentGatewayConfig from configuration before applying optional overrides.
    /// </summary>
    public static IServiceCollection AddPayBridge(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PaymentGatewayConfig>? configAction = null)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return AddPayBridgeCore(services, configuration, configAction);
    }

    private static IServiceCollection AddPayBridgeCore(
        IServiceCollection services,
        IConfiguration? configuration,
        Action<PaymentGatewayConfig>? configAction)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var config = new PaymentGatewayConfig();
        configuration?.GetSection("PaymentGatewayConfig").Bind(config);
        configAction?.Invoke(config);

        services.AddSingleton(config);
        services.AddSingleton<IOptions<PaymentGatewayConfig>>(Options.Create(config));

        // Register core services
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<PaymentGatewayFactory>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
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
        // If no gateways are explicitly enabled, enable all configured ones.
        if (config.EnabledGateways.Count == 0)
        {
            foreach (var gateway in Enum.GetValues<PaymentGatewayType>())
            {
                if (gateway != PaymentGatewayType.Automatic && IsGatewayConfigured(config, gateway))
                {
                    RegisterGateway(services, gateway);
                }
            }

            return;
        }

        // Register only the enabled gateways
        foreach (var gateway in config.EnabledGateways)
        {
            RegisterGateway(services, gateway);
        }
    }

    private static void RegisterGateway(IServiceCollection services, PaymentGatewayType gateway)
    {
        switch (gateway)
        {
            case PaymentGatewayType.Stripe:
                AddGatewayRegistration<StripeGateway>(services);
                break;
            case PaymentGatewayType.Paystack:
                AddGatewayRegistration<PaystackGateway>(services);
                break;
            case PaymentGatewayType.Flutterwave:
                AddGatewayRegistration<FlutterwaveGateway>(services);
                break;
            case PaymentGatewayType.Checkout:
                AddGatewayRegistration<CheckoutGateway>(services);
                break;
            case PaymentGatewayType.BenefitPay:
                AddGatewayRegistration<BenefitPayGateway>(services);
                break;
            case PaymentGatewayType.Knet:
                AddGatewayRegistration<KnetGateway>(services);
                break;
            case PaymentGatewayType.Monnify:
                AddGatewayRegistration<MonnifyGateway>(services);
                break;
            case PaymentGatewayType.Squad:
                AddGatewayRegistration<SquadGateway>(services);
                break;
            case PaymentGatewayType.Korapay:
                AddGatewayRegistration<KorapayGateway>(services);
                break;
            case PaymentGatewayType.Interswitch:
                AddGatewayRegistration<InterswitchGateway>(services);
                break;
            case PaymentGatewayType.Remita:
                AddGatewayRegistration<RemitaGateway>(services);
                break;
            case PaymentGatewayType.Opay:
                AddGatewayRegistration<OpayGateway>(services);
                break;
            case PaymentGatewayType.DpoGroup:
                AddGatewayRegistration<DpoGroupGateway>(services);
                break;
            case PaymentGatewayType.PawaPay:
                AddGatewayRegistration<PawaPayGateway>(services);
                break;
            case PaymentGatewayType.PeachPayments:
                AddGatewayRegistration<PeachPaymentsGateway>(services);
                break;
        }
    }

    private static void AddGatewayRegistration<TGateway>(IServiceCollection services)
        where TGateway : class, IPaymentGateway
    {
        services.AddScoped<TGateway>();
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<TGateway>());
    }

    private static string NormalizeProviderName(string provider)
    {
        return provider.Trim().ToLowerInvariant() switch
        {
            "mssql" => "SqlServer",
            "sqlserver" => "SqlServer",
            "postgres" => "Npgsql",
            "postgresql" => "Npgsql",
            "npgsql" => "Npgsql",
            "mysql" => "MySql",
            _ => provider
        };
    }

    private static bool IsGatewayConfigured(PaymentGatewayConfig config, PaymentGatewayType gateway)
    {
        return gateway switch
        {
            PaymentGatewayType.Paystack => HasValue(config.Paystack.SecretKey),
            PaymentGatewayType.Flutterwave => HasValue(config.FlutterwaveConfig.SecretKey),
            PaymentGatewayType.Stripe => HasValue(config.Stripe.SecretKey),
            PaymentGatewayType.Checkout => HasValue(config.Checkout.SecretKey),
            PaymentGatewayType.BenefitPay => HasValue(config.BenefitPay.MerchantId) && HasValue(config.BenefitPay.ApiKey),
            PaymentGatewayType.Knet => HasValue(config.Knet.TransportId) && HasValue(config.Knet.Password),
            PaymentGatewayType.Monnify => HasValue(config.Monnify.ApiKey) && HasValue(config.Monnify.SecretKey) && HasValue(config.Monnify.ContractCode),
            PaymentGatewayType.Squad => HasValue(config.Squad.SecretKey),
            PaymentGatewayType.Korapay => HasValue(config.Korapay.SecretKey),
            PaymentGatewayType.Interswitch => HasValue(config.Interswitch.ClientId) && HasValue(config.Interswitch.ClientSecret) && HasValue(config.Interswitch.MerchantCode),
            PaymentGatewayType.Remita => HasValue(config.Remita.MerchantId) && HasValue(config.Remita.ServiceTypeId) && HasValue(config.Remita.ApiKey),
            PaymentGatewayType.Opay => HasValue(config.Opay.MerchantId) && HasValue(config.Opay.SecretKey),
            PaymentGatewayType.DpoGroup => HasValue(config.DpoGroup.CompanyToken),
            PaymentGatewayType.PawaPay => HasValue(config.PawaPay.ApiToken),
            PaymentGatewayType.PeachPayments => HasValue(config.PeachPayments.EntityId) && HasValue(config.PeachPayments.AccessToken),
            _ => false
        };
    }

    private static bool HasValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }
}
