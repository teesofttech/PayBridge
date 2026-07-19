namespace PayBridge.SDK.Application.Dtos.Request;
public class RefundRequest
{
    public string? IdempotencyKey { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}