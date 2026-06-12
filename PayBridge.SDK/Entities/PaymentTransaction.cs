using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Entities;
public class PaymentTransaction
{
    public string Id { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public PaymentGatewayType Gateway { get; set; }
    public string GatewayResponse { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
