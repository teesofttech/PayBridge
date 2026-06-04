namespace PayBridge.SDK.Enums;
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
    Knet = 6,

    /// <summary>
    /// Monnify payment gateway (Nigeria)
    /// </summary>
    Monnify = 7,

    /// <summary>
    /// Squad payment gateway by GTCo (Nigeria)
    /// </summary>
    Squad = 8,

    /// <summary>
    /// Korapay payment gateway (Nigeria/Africa)
    /// </summary>
    Korapay = 9,

    /// <summary>
    /// Interswitch payment gateway (Nigeria - Quickteller / Webpay)
    /// </summary>
    Interswitch = 10,

    /// <summary>
    /// Remita payment gateway (Nigeria)
    /// </summary>
    Remita = 11,

    /// <summary>
    /// OPay payment gateway (Nigeria / Africa)
    /// </summary>
    Opay = 12
}