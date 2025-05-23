using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class PaymentRequest
{
    public string? Logo { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Description { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string RedirectUrl { get; set; }
    public string WebhookUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Card;
    public string SavedPaymentMethodId { get; set; }
    public string? AppName { get; set; }
}
