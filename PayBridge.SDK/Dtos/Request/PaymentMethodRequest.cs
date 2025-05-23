using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class PaymentMethodRequest
{
    public string CustomerEmail { get; set; }
    public string CustomerName { get; set; }
    public string Token { get; set; }
    public PaymentMethodType Type { get; set; }
    public bool IsDefault { get; set; }
}