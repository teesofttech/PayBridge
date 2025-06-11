using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Presentation.Models;

namespace PayBridge.SDK.Presentation.Mapper;

public static class PaymentRequestMapper
{
    public static PaymentRequest MapToPaymentRequest(CreatePaymentRequest request)
    {
        return new PaymentRequest
        {
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            CustomerEmail = request.CustomerEmail,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            RedirectUrl = request.RedirectUrl,
            WebhookUrl = request.WebhookUrl,
            Metadata = request.Metadata,
            PaymentMethodType = request.PaymentMethodType,
            SavedPaymentMethodId = request.SavedPaymentMethodId
        };
    }
}
