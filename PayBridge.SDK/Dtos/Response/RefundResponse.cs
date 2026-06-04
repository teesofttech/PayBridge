using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Response;
public class RefundResponse
{
    public bool Success { get; set; }
    public string RefundReference { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime RefundDate { get; set; }
}
