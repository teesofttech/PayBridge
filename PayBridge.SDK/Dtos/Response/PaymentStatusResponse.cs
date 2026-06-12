namespace PayBridge.SDK.Dtos.Response;
public class PaymentStatusResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string GatewayResponse { get; set; } = string.Empty;
}
