namespace PayBridge.SDK.Domain.Enums;
/// <summary>
/// Supported payment gateway providers
/// </summary>
public enum PaymentGatewayType
{
    /// <summary>
    /// Automatically select the best gateway
    /// </summary>
    Automatic = 0,

    /// <summary>
    /// Flutterwave payment gateway
    /// </summary>
    Flutterwave = 1,

    /// <summary>
    /// Paystack payment gateway
    /// </summary>
    Paystack = 2,

    /// <summary>
    /// Stripe payment gateway
    /// </summary>
    Stripe = 3,

    /// <summary>
    /// Checkout.com payment gateway
    /// </summary>
    Checkout = 4,

    /// <summary>
    /// BenefitPay payment gateway
    /// </summary>
    BenefitPay = 5,

    /// <summary>
    /// Knet payment gateway
    /// </summary>
    Knet = 6
}