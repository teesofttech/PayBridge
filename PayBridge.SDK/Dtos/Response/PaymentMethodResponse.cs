namespace PayBridge.SDK.Dtos.Response;
public class PaymentMethodResponse
{
    public bool Success { get; set; }
    public string PaymentMethodId { get; set; }
    public string Message { get; set; }
    public string Last4 { get; set; }
    public string ExpiryMonth { get; set; }
    public string ExpiryYear { get; set; }
    public string Brand { get; set; }
}
