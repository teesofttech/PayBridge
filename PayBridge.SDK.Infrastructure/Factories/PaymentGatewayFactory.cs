using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Domain.Enums;
using PayBridge.SDK.Infrastructure.Gateways;

namespace PayBridge.SDK.Infrastructure.Factories;
public class PaymentGatewayFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PaymentGatewayConfig _config;
    private readonly ILogger<PaymentGatewayFactory> _logger;

    public PaymentGatewayFactory(
        IServiceProvider serviceProvider,
        PaymentGatewayConfig config,
        ILogger<PaymentGatewayFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates instances of all configured payment gateways
    /// </summary>
    /// <returns>Dictionary of gateway instances by type</returns>
    public Dictionary<PaymentGatewayType, IPaymentGateway> CreateGateways()
    {
        var gateways = new Dictionary<PaymentGatewayType, IPaymentGateway>();

        // If no gateways are explicitly enabled, enable all available ones
        if (_config.EnabledGateways.Count == 0)
        {
            _logger.LogInformation("No gateways explicitly enabled, enabling all available gateways");

            // Try to initialize all gateways
            TryAddGateway(gateways, PaymentGatewayType.Stripe);
            TryAddGateway(gateways, PaymentGatewayType.Paystack);
            TryAddGateway(gateways, PaymentGatewayType.Flutterwave);
            TryAddGateway(gateways, PaymentGatewayType.Checkout);
            TryAddGateway(gateways, PaymentGatewayType.BenefitPay);
            TryAddGateway(gateways, PaymentGatewayType.Knet);
        }
        else
        {
            _logger.LogInformation("Enabling {Count} specified gateways", _config.EnabledGateways.Count);

            // Initialize only the enabled gateways
            foreach (var gatewayType in _config.EnabledGateways)
            {
                if (gatewayType != PaymentGatewayType.Automatic)
                {
                    TryAddGateway(gateways, gatewayType);
                }
            }
        }

        if (gateways.Count == 0)
        {
            _logger.LogWarning("No payment gateways were successfully initialized");
        }
        else
        {
            _logger.LogInformation("Successfully initialized {Count} payment gateways", gateways.Count);
        }

        return gateways;
    }

    private void TryAddGateway(Dictionary<PaymentGatewayType, IPaymentGateway> gateways, PaymentGatewayType gatewayType)
    {
        try
        {
            IPaymentGateway gateway = gatewayType switch
            {
                //PaymentGatewayType.Stripe => _serviceProvider.GetService<StripeGateway>(),
                PaymentGatewayType.Paystack => _serviceProvider.GetService<PaystackGateway>(),
                //PaymentGatewayType.Flutterwave => _serviceProvider.GetService<FlutterwaveGateway>(),
                //PaymentGatewayType.Checkout => _serviceProvider.GetService<CheckoutGateway>(),
                //PaymentGatewayType.BenefitPay => _serviceProvider.GetService<BenefitPayGateway>(),
                //PaymentGatewayType.Knet => _serviceProvider.GetService<KnetGateway>(),
                _ => null
            };

            if (gateway != null)
            {
                gateways[gatewayType] = gateway;
                _logger.LogInformation("Successfully initialized {Gateway} gateway", gatewayType);
            }
            else
            {
                _logger.LogWarning("{Gateway} gateway is not registered in the service provider", gatewayType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {Gateway} gateway", gatewayType);
        }
    }
}
