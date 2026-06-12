using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class VerificationRequest
{
    public string TransactionReference { get; set; } = string.Empty;
    public PaymentGatewayType Gateway { get; set; } = PaymentGatewayType.Automatic;
}