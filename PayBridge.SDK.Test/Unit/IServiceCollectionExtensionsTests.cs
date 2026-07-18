using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

[Trait("Category", "Unit")]
public class IServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPayBridge_BindsConfiguration_AndAppliesCodeOverrides_ToSingletonAndOptions()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PaymentGatewayConfig:DefaultGateway"] = "Stripe",
            ["PaymentGatewayConfig:EnabledGateways:0"] = "Paystack",
            ["PaymentGatewayConfig:Paystack:SecretKey"] = "sk_from_config"
        });

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPayBridge(configuration, config =>
        {
            config.Paystack.SecretKey = "sk_from_code";
        });

        using var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<PaymentGatewayConfig>();
        var options = provider.GetRequiredService<IOptions<PaymentGatewayConfig>>();

        config.DefaultGateway.Should().Be(PaymentGatewayType.Stripe);
        config.EnabledGateways.Should().ContainSingle().Which.Should().Be(PaymentGatewayType.Paystack);
        config.Paystack.SecretKey.Should().Be("sk_from_code");
        options.Value.Should().BeSameAs(config);
    }

    [Fact]
    public void AddPayBridge_RegistersEnabledGateway_AsIPaymentGateway()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PaymentGatewayConfig:EnabledGateways:0"] = "PeachPayments",
            ["PaymentGatewayConfig:PeachPayments:EntityId"] = "test_entity",
            ["PaymentGatewayConfig:PeachPayments:AccessToken"] = "test_token"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPayBridge(configuration);

        using var provider = services.BuildServiceProvider();

        var gateways = provider.GetServices<IPaymentGateway>().ToList();

        gateways.Should().ContainSingle();
        gateways[0].GatewayType.Should().Be(PaymentGatewayType.PeachPayments);
    }

    [Fact]
    public void AddPayBridge_WhenEnabledGatewaysIsEmpty_RegistersOnlyConfiguredGateways()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PaymentGatewayConfig:Paystack:SecretKey"] = "sk_test_paystack",
            ["PaymentGatewayConfig:Stripe:SecretKey"] = "YOUR_STRIPE_SECRET_KEY"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPayBridge(configuration);

        using var provider = services.BuildServiceProvider();

        var gateways = provider.GetServices<IPaymentGateway>().ToList();

        gateways.Should().ContainSingle();
        gateways[0].GatewayType.Should().Be(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void AddPayBridge_RegistersWebhookVerifier_AndPreservesCustomTimeProvider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PaymentGatewayConfig:Paystack:SecretKey"] = "sk_test_paystack"
        });
        var customTimeProvider = new TestTimeProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(customTimeProvider);

        services.AddPayBridge(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWebhookSignatureVerifier>().Should().NotBeNull();
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(customTimeProvider);
    }

    [Fact]
    public void AddPayBridge_RegistersRefundRepository()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPayBridge(BuildConfiguration([]));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IRefundRepository) &&
            descriptor.ImplementationType == typeof(RefundRepository) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestTimeProvider : TimeProvider
    {
    }
}
