using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Test.Helpers;

/// <summary>
/// Builds valid PaymentRequest and RefundRequest objects with sensible test defaults.
/// Override individual properties as needed in each test.
/// </summary>
public static class PaymentRequestFactory
{
    public static PaymentRequest Build(Action<PaymentRequest>? configure = null)
    {
        var request = new PaymentRequest
        {
            Amount          = 5000.00m,
            Currency        = "NGN",
            Description     = "Test payment",
            CustomerEmail   = "test@paybridge.dev",
            CustomerName    = "Test User",
            CustomerPhone   = "+2348000000000",
            RedirectUrl     = "https://paybridge.dev/callback",
            WebhookUrl      = "https://paybridge.dev/webhook",
            PaymentMethodType = PaymentMethodType.Card,
            Metadata        = new Dictionary<string, string>
            {
                ["order_id"] = "ORD-001",
                ["source"]   = "unit-test"
            }
        };

        configure?.Invoke(request);
        return request;
    }

    public static RefundRequest BuildRefund(Action<RefundRequest>? configure = null)
    {
        var request = new RefundRequest
        {
            TransactionReference = "TEST_REF_001",
            Amount               = 5000.00m,
            Reason               = "Customer requested refund"
        };

        configure?.Invoke(request);
        return request;
    }
}

/// <summary>
/// Builds <see cref="PaymentGatewayConfig"/> objects with a single gateway configured.
/// Each method only sets the minimum keys needed to pass the gateway's constructor validation.
/// </summary>
public static class GatewayConfigFactory
{
    public static PaymentGatewayConfig BuildPaystack(
        string secretKey = "sk_test_paystack_secret") =>
        new() { Paystack = new PaystackConfig { SecretKey = secretKey } };

    public static PaymentGatewayConfig BuildFlutterwave(
        string secretKey = "FLWSECK_TEST-flutterwave-secret") =>
        new() { FlutterwaveConfig = new FlutterwaveConfig { SecretKey = secretKey } };

    public static PaymentGatewayConfig BuildStripe(
        string secretKey = "sk_test_stripe_secret") =>
        new() { Stripe = new StripeConfig { SecretKey = secretKey } };

    public static PaymentGatewayConfig BuildCheckout(
        string secretKey = "sk_test_checkout_secret") =>
        new() { Checkout = new CheckoutConfig { SecretKey = secretKey } };

    public static PaymentGatewayConfig BuildBenefitPay(
        string merchantId = "test_merchant",
        string apiKey     = "test_api_key") =>
        new() { BenefitPay = new BenefitPayConfig { MerchantId = merchantId, ApiKey = apiKey } };

    public static PaymentGatewayConfig BuildKnet(
        string transportId = "test_transport",
        string password    = "test_password") =>
        new() { Knet = new KnetConfig { TransportId = transportId, Password = password } };

    public static PaymentGatewayConfig BuildMonnify(
        string apiKey       = "MK_TEST_monnify_api_key",
        string secretKey    = "test_monnify_secret",
        string contractCode = "TEST_CONTRACT") =>
        new()
        {
            Monnify = new MonnifyConfig
            {
                ApiKey       = apiKey,
                SecretKey    = secretKey,
                ContractCode = contractCode
            }
        };

    public static PaymentGatewayConfig BuildSquad(
        string secretKey = "test_squad_secret",
        bool isSandbox   = true) =>
        new() { Squad = new SquadConfig { SecretKey = secretKey, IsSandbox = isSandbox } };

    public static PaymentGatewayConfig BuildKorapay(
        string secretKey = "sk_test_korapay_secret") =>
        new() { Korapay = new KorapayConfig { SecretKey = secretKey } };

    public static PaymentGatewayConfig BuildInterswitch(
        string clientId     = "test_client_id",
        string clientSecret = "test_client_secret",
        bool isSandbox      = true) =>
        new()
        {
            Interswitch = new InterswitchConfig
            {
                ClientId     = clientId,
                ClientSecret = clientSecret,
                IsSandbox    = isSandbox
            }
        };

    public static PaymentGatewayConfig BuildRemita(
        string merchantId   = "test_merchant_id",
        string serviceTypeId = "test_service_type",
        string apiKey       = "test_remita_api_key",
        bool isSandbox      = true) =>
        new()
        {
            Remita = new RemitaConfig
            {
                MerchantId    = merchantId,
                ServiceTypeId = serviceTypeId,
                ApiKey        = apiKey,
                IsSandbox     = isSandbox
            }
        };

    public static PaymentGatewayConfig BuildOpay(
        string merchantId = "test_merchant_id",
        string publicKey  = "test_public_key",
        string secretKey  = "test_opay_secret",
        bool isSandbox    = true) =>
        new()
        {
            Opay = new OpayConfig
            {
                MerchantId = merchantId,
                PublicKey  = publicKey,
                SecretKey  = secretKey,
                IsSandbox  = isSandbox
            }
        };

    public static PaymentGatewayConfig BuildDpoGroup(
        string companyToken     = "TEST_DPO_COMPANY_TOKEN",
        string paymentCurrency  = "USD",
        bool isSandbox          = true) =>
        new()
        {
            DpoGroup = new DpoGroupConfig
            {
                CompanyToken    = companyToken,
                PaymentCurrency = paymentCurrency,
                IsSandbox       = isSandbox
            }
        };

    public static PaymentGatewayConfig BuildPawaPay(
        string apiToken = "test_pawapay_token",
        bool isSandbox  = true) =>
        new() { PawaPay = new PawaPayConfig { ApiToken = apiToken, IsSandbox = isSandbox } };

    public static PaymentGatewayConfig BuildPeachPayments(
        string entityId    = "test_entity_id",
        string accessToken = "test_access_token",
        bool isSandbox     = true) =>
        new()
        {
            PeachPayments = new PeachPaymentsConfig
            {
                EntityId    = entityId,
                AccessToken = accessToken,
                IsSandbox   = isSandbox
            }
        };
}
