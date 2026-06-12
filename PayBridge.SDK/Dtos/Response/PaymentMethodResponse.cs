namespace PayBridge.SDK.Dtos.Response;
public class PaymentMethodResponse
{
    public bool Success { get; set; }
    public string PaymentMethodId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
}
