using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Response;
public class VerificationResponse
{
    public bool Success { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Fee { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal? AmountSettled { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> GatewayResponse { get; set; } = new Dictionary<string, string>();
}