using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class PaymentMethodRequest
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public PaymentMethodType Type { get; set; }
    public bool IsDefault { get; set; }
}