namespace PayBridge.SDK.Dtos.Response;
public class PaymentStatusResponse
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public string GatewayResponse { get; set; }
}
