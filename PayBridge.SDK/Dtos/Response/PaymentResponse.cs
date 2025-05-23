using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Response;
public class PaymentResponse
{
    public bool Success { get; set; }
    public string TransactionReference { get; set; }
    public string Message { get; set; }
    public string CheckoutUrl { get; set; }
    public PaymentStatus Status { get; set; }
    public Dictionary<string, string> GatewayResponse { get; set; } = new Dictionary<string, string>();
}
