using PayBridge.SDK.Domain.Enums;

namespace PayBridge.SDK.Domain.Entities;
public class PaymentTransaction
{
    public string Id { get; set; }
    public string TransactionReference { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerName { get; set; }
    public PaymentStatus Status { get; set; }
    public PaymentGatewayType Gateway { get; set; }
    public string GatewayResponse { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
