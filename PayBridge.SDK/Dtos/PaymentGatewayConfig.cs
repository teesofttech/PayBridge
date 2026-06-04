using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Application.Dtos;

public class PaymentGatewayConfig
{
    public PaymentGatewayType DefaultGateway { get; set; } = PaymentGatewayType.Automatic;
    public List<PaymentGatewayType> EnabledGateways { get; set; } = new List<PaymentGatewayType>();
    public PaystackConfig Paystack { get; set; } = new();
    public FlutterwaveConfig FlutterwaveConfig { get; set; } = new();
    public StripeConfig Stripe { get; set; } = new();
    public CheckoutConfig Checkout { get; set; } = new();
    public BenefitPayConfig BenefitPay { get; set; } = new();
    public KnetConfig Knet { get; set; } = new();
}

public class PaystackConfig
{
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public class FlutterwaveConfig
{
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
}

public class StripeConfig
{
    public string SecretKey { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2023-10-16";
}

public class CheckoutConfig
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}

public class BenefitPayConfig
{
    public string MerchantId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}

public class KnetConfig
{
    public string TransportId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TerminalResourceKey { get; set; } = string.Empty;
}

