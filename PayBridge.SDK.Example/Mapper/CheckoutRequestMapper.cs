using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Example.Models;

namespace PayBridge.SDK.Example.Mapper;

/// <summary>
/// Maps the example-project's <see cref="CheckoutRequest"/> to the SDK's
/// <see cref="PaymentRequest"/> without leaking SDK types into the API surface.
/// </summary>
public static class CheckoutRequestMapper
{
    public static PaymentRequest ToPaymentRequest(CheckoutRequest src) => new()
    {
        Amount               = src.Amount,
        Currency             = src.Currency,
        Description          = src.Description,
        CustomerEmail        = src.CustomerEmail,
        CustomerName         = src.CustomerName,
        CustomerPhone        = src.CustomerPhone,
        RedirectUrl          = src.RedirectUrl,
        WebhookUrl           = src.WebhookUrl,
        Metadata             = src.Metadata,
        PaymentMethodType    = src.PaymentMethodType,
    };
}
