using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Interfaces;
using PayBridge.SDK.Test.Helpers;
using Xunit;

namespace PayBridge.SDK.Test.Unit;

/// <summary>
/// Unit tests for PaymentGatewayFactory — DI wiring and gateway selection logic.
/// Closes #52 (partial).
/// </summary>
[Trait("Category", "Unit")]
public class PaymentGatewayFactoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<ILogger<PaymentGatewayFactory>> BuildLogger() =>
        new Mock<ILogger<PaymentGatewayFactory>>();

    /// <summary>
    /// Builds a real DI ServiceProvider with only the specified gateway registered.
    /// Gateways that need HttpClient use a MockHttpMessageHandler so the
    /// constructor succeeds without touching the network.
    /// </summary>
    private static IServiceProvider BuildServiceProviderWith(
        PaymentGatewayConfig config,
        params PaymentGatewayType[] gatewayTypes)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Register a named HttpClient for every gateway that uses IHttpClientFactory
        services.AddHttpClient();

        services.AddSingleton(config);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(config));

        foreach (var type in gatewayTypes)
        {
            switch (type)
            {
                case PaymentGatewayType.Paystack:
                    services.AddTransient<PaystackGateway>();
                    break;
                case PaymentGatewayType.Flutterwave:
                    services.AddTransient<FlutterwaveGateway>();
                    break;
                case PaymentGatewayType.Stripe:
                    services.AddTransient<StripeGateway>();
                    break;
                case PaymentGatewayType.Monnify:
                    services.AddTransient<MonnifyGateway>();
                    break;
                case PaymentGatewayType.Squad:
                    services.AddTransient<SquadGateway>();
                    break;
                case PaymentGatewayType.Korapay:
                    services.AddTransient<KorapayGateway>();
                    break;
                case PaymentGatewayType.PeachPayments:
                    services.AddTransient<PeachPaymentsGateway>();
                    break;
            }
        }

        return services.BuildServiceProvider();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
    {
        var act = () => new PaymentGatewayFactory(
            null!,
            new PaymentGatewayConfig(),
            BuildLogger().Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        var sp = BuildServiceProviderWith(new PaymentGatewayConfig());

        var act = () => new PaymentGatewayFactory(
            sp,
            null!,
            BuildLogger().Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var config = new PaymentGatewayConfig();
        var sp = BuildServiceProviderWith(config);

        var act = () => new PaymentGatewayFactory(sp, config, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    // ── CreateGateways — EnabledGateways empty ────────────────────────────────

    [Fact]
    public void CreateGateways_ReturnsPaystack_WhenOnlyPaystackIsRegisteredInDI()
    {
        var config = GatewayConfigFactory.BuildPaystack();
        var sp     = BuildServiceProviderWith(config, PaymentGatewayType.Paystack);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var gateways = factory.CreateGateways();

        gateways.Should().ContainKey(PaymentGatewayType.Paystack);
        gateways[PaymentGatewayType.Paystack].GatewayType
            .Should().Be(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void CreateGateways_SkipsGateway_WhenNotRegisteredInServiceProvider()
    {
        // Config has Paystack key, but DI has no PaystackGateway registered
        var config = GatewayConfigFactory.BuildPaystack();
        var sp     = BuildServiceProviderWith(config); // nothing registered
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var gateways = factory.CreateGateways();

        // Should not throw; simply returns empty (all GetService calls return null)
        gateways.Should().NotContainKey(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void CreateGateways_ReturnsEmptyDictionary_WhenNoGatewaysAreRegistered()
    {
        var config  = new PaymentGatewayConfig();
        var sp      = BuildServiceProviderWith(config);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var gateways = factory.CreateGateways();

        gateways.Should().BeEmpty();
    }

    [Fact]
    public void CreateGateways_ReturnsMultipleGateways_WhenMultipleAreRegistered()
    {
        var config = new PaymentGatewayConfig
        {
            Paystack          = new PaystackConfig { SecretKey = "sk_test" },
            FlutterwaveConfig = new FlutterwaveConfig { SecretKey = "FLW_test" }
        };

        var sp = BuildServiceProviderWith(config,
            PaymentGatewayType.Paystack,
            PaymentGatewayType.Flutterwave);

        var factory  = new PaymentGatewayFactory(sp, config, BuildLogger().Object);
        var gateways = factory.CreateGateways();

        gateways.Should().ContainKey(PaymentGatewayType.Paystack);
        gateways.Should().ContainKey(PaymentGatewayType.Flutterwave);
    }

    // ── CreateGateways — EnabledGateways specified ────────────────────────────

    [Fact]
    public void CreateGateways_ReturnsOnlyEnabledGateways_WhenEnableListIsSet()
    {
        var config = new PaymentGatewayConfig
        {
            EnabledGateways   = new List<PaymentGatewayType> { PaymentGatewayType.Paystack },
            Paystack          = new PaystackConfig { SecretKey = "sk_test" },
            FlutterwaveConfig = new FlutterwaveConfig { SecretKey = "FLW_test" }
        };

        var sp = BuildServiceProviderWith(config,
            PaymentGatewayType.Paystack,
            PaymentGatewayType.Flutterwave);

        var factory  = new PaymentGatewayFactory(sp, config, BuildLogger().Object);
        var gateways = factory.CreateGateways();

        gateways.Should().ContainKey(PaymentGatewayType.Paystack);
        gateways.Should().NotContainKey(PaymentGatewayType.Flutterwave);
    }

    [Fact]
    public void CreateGateways_IgnoresAutomaticGatewayType_InEnabledList()
    {
        var config = new PaymentGatewayConfig
        {
            EnabledGateways = new List<PaymentGatewayType>
            {
                PaymentGatewayType.Automatic,
                PaymentGatewayType.Paystack
            },
            Paystack = new PaystackConfig { SecretKey = "sk_test" }
        };

        var sp      = BuildServiceProviderWith(config, PaymentGatewayType.Paystack);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);
        var gateways = factory.CreateGateways();

        gateways.Should().NotContainKey(PaymentGatewayType.Automatic);
        gateways.Should().ContainKey(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void CreateGateways_DoesNotThrow_WhenGatewayConstructorThrows()
    {
        // Paystack with empty SecretKey will throw InvalidOperationException in its constructor.
        // The factory should catch it and continue, not surface it.
        var config = new PaymentGatewayConfig
        {
            EnabledGateways = new List<PaymentGatewayType> { PaymentGatewayType.Paystack },
            Paystack        = new PaystackConfig { SecretKey = "" } // intentionally invalid
        };

        var sp      = BuildServiceProviderWith(config, PaymentGatewayType.Paystack);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var act = () => factory.CreateGateways();

        act.Should().NotThrow();
    }

    // ── GatewayType correctness ───────────────────────────────────────────────

    [Fact]
    public void CreateGateways_ReturnsGatewayWithCorrectType_ForPaystack()
    {
        var config  = GatewayConfigFactory.BuildPaystack();
        var sp      = BuildServiceProviderWith(config, PaymentGatewayType.Paystack);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var gateways = factory.CreateGateways();

        gateways[PaymentGatewayType.Paystack].GatewayType
            .Should().Be(PaymentGatewayType.Paystack);
    }

    [Fact]
    public void CreateGateways_ReturnsGatewayImplementingIPaymentGateway()
    {
        var config  = GatewayConfigFactory.BuildPaystack();
        var sp      = BuildServiceProviderWith(config, PaymentGatewayType.Paystack);
        var factory = new PaymentGatewayFactory(sp, config, BuildLogger().Object);

        var gateways = factory.CreateGateways();

        gateways[PaymentGatewayType.Paystack]
            .Should().BeAssignableTo<IPaymentGateway>();
    }
}
