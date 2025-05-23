using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class VerificationRequest
{
    public string TransactionReference { get; set; }
    public PaymentGatewayType Gateway { get; set; } = PaymentGatewayType.Automatic;
}