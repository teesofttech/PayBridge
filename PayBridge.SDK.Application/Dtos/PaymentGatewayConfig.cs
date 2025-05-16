using PayBridge.SDK.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos;
public class PaymentGatewayConfig
{
    public string ApiKey { get; set; }
    public string SecretKey { get; set; }
    public string MerchantId { get; set; }
    public Dictionary<string, string> AdditionalParameters { get; set; } = new Dictionary<string, string>();
    public PaymentGatewayType DefaultGateway { get; set; } = PaymentGatewayType.Automatic;
    public List<PaymentGatewayType> EnabledGateways { get; set; } = new List<PaymentGatewayType>();

    // Gateway-specific configurations
    public string PaystackPublicKey => AdditionalParameters.TryGetValue("paystack_public_key", out var key) ? key : null;
    public string FlutterwaveEncryptionKey => AdditionalParameters.TryGetValue("flutterwave_encryption_key", out var key) ? key : null;
    public string StripeApiVersion => AdditionalParameters.TryGetValue("stripe_version", out var version) ? version : "2023-10-16";

}
