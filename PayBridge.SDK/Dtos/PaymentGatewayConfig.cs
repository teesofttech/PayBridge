using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Application.Dtos;

public class PaymentGatewayConfig
{
    public PaymentGatewayType DefaultGateway { get; set; } = PaymentGatewayType.Automatic;
    public List<PaymentGatewayType> EnabledGateways { get; set; } = new List<PaymentGatewayType>();
    public PaystackConfig Paystack { get; set; }
    public FlutterwaveConfig FlutterwaveConfig { get; set; }
    public StripeConfig Stripe { get; set; }
}

public class PaystackConfig
{
    public string PublicKey { get; set; }
    public string SecretKey { get; set; }
}

public class FlutterwaveConfig
{
    public string PublicKey { get; set; }
    public string SecretKey { get; set; }
    public string EncryptionKey { get; set; }
}

public class StripeConfig
{
    public string SecretKey { get; set; }
    public string ApiVersion { get; set; } = "2023-10-16";
}

