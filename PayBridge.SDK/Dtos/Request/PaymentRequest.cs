using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Dtos.Request;
public class PaymentRequest
{
    public string? Logo { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Card;
    public string SavedPaymentMethodId { get; set; } = string.Empty;
    public string? AppName { get; set; }
}
