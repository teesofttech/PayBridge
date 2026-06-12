using Microsoft.AspNetCore.Mvc;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Example.Models;
using PayBridge.SDK.Interfaces;

namespace PayBridge.SDK.Example.Controllers;

/// <summary>
/// SCENARIO 3 — Multiple gateways active simultaneously.
///
/// Shows how to:
/// 1. Configure several gateways at once (done in Program.cs / appsettings.json).
/// 2. List which gateways are currently active.
/// 3. Let the caller switch gateways at request time — useful when you want to
///    offer the customer a choice ("Pay with Paystack" / "Pay with Flutterwave").
/// 4. Understand which currencies each gateway handles best.
/// </summary>
[ApiController]
[Route("api/gateways")]
[Produces("application/json")]
public class MultiGatewayController : ControllerBase
{
    private readonly PaymentGatewayConfig _config;
    private readonly ILogger<MultiGatewayController> _log;

    public MultiGatewayController(
        PaymentGatewayConfig config,
        ILogger<MultiGatewayController> logger)
    {
        _config = config;
        _log    = logger;
    }

    // ── GET /api/gateways ────────────────────────────────────────────────────

    /// <summary>
    /// Lists all gateways that are currently active (registered in DI and
    /// present in the <c>EnabledGateways</c> configuration list).
    ///
    /// Use this endpoint to:
    /// - Debug which gateways are wired up.
    /// - Drive a "choose your payment method" UI by fetching this list and
    ///   rendering a button for each entry.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult ListActiveGateways()
    {
        // The EnabledGateways list mirrors exactly what AddPayBridge() registered.
        // Automatic (0) is a routing directive, not a real gateway — filter it out.
        var active = _config.EnabledGateways
            .Where(g => g != PaymentGatewayType.Automatic)
            .Select(g => new
            {
                id          = (int)g,
                name        = g.ToString(),
                currencies  = SuggestedCurrencies(g),
                description = GatewayDescription(g),
            })
            .ToList();

        _log.LogInformation("{Count} gateway(s) active", active.Count);

        return Ok(ApiResponse<object>.Ok(new
        {
            count    = active.Count,
            gateways = active,
            tip      = "Pass the 'id' value as the 'gateway' field in POST /api/payment " +
                       "to force that specific gateway.",
        }));
    }

    // ── GET /api/gateways/{id} ───────────────────────────────────────────────

    /// <summary>
    /// Returns configuration details for a single gateway.
    /// Useful for confirming that the SDK loaded the API keys you supplied.
    ///
    /// <b>Note:</b> secret keys are never returned — only their presence is confirmed.
    /// </summary>
    /// <param name="id">The numeric <see cref="PaymentGatewayType"/> value.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public IActionResult GetGatewayInfo(int id)
    {
        if (!Enum.IsDefined(typeof(PaymentGatewayType), id) || id == 0)
            return NotFound(ApiResponse<object>.Fail($"No gateway with id={id}.", "NOT_FOUND"));

        var type = (PaymentGatewayType)id;
        var isEnabled = _config.EnabledGateways.Contains(type);

        var keyStatus = KeyStatus(type);

        return Ok(ApiResponse<object>.Ok(new
        {
            id          = id,
            name        = type.ToString(),
            isEnabled   = isEnabled,
            currencies  = SuggestedCurrencies(type),
            keys        = keyStatus,
            description = GatewayDescription(type),
        }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns which currency codes work best with each gateway.
    /// This is guidance only — most gateways support multiple currencies.
    /// </summary>
    private static string[] SuggestedCurrencies(PaymentGatewayType g) => g switch
    {
        PaymentGatewayType.Paystack     => new[] { "NGN", "GHS", "ZAR", "USD", "KES" },
        PaymentGatewayType.Flutterwave  => new[] { "NGN", "GHS", "KES", "ZAR", "UGX", "USD", "EUR", "GBP" },
        PaymentGatewayType.Stripe       => new[] { "USD", "EUR", "GBP", "AUD", "CAD" },
        PaymentGatewayType.Korapay      => new[] { "NGN", "GHS", "KES" },
        PaymentGatewayType.Monnify      => new[] { "NGN" },
        PaymentGatewayType.Squad        => new[] { "NGN" },
        PaymentGatewayType.Interswitch  => new[] { "NGN" },
        PaymentGatewayType.Remita       => new[] { "NGN" },
        PaymentGatewayType.Opay         => new[] { "NGN" },
        PaymentGatewayType.PeachPayments=> new[] { "ZAR", "KES", "NGN", "BWP", "USD" },
        PaymentGatewayType.DpoGroup     => new[] { "KES", "GHS", "UGX", "ZAR", "USD" },
        PaymentGatewayType.PawaPay      => new[] { "GHS", "TZS", "UGX", "RWF", "ZMW" },
        PaymentGatewayType.Checkout     => new[] { "USD", "EUR", "GBP", "AED" },
        PaymentGatewayType.BenefitPay   => new[] { "BHD" },
        PaymentGatewayType.Knet         => new[] { "KWD" },
        _                               => Array.Empty<string>(),
    };

    private static string GatewayDescription(PaymentGatewayType g) => g switch
    {
        PaymentGatewayType.Paystack     => "Nigeria & Africa — cards, bank transfer, USSD",
        PaymentGatewayType.Flutterwave  => "Pan-Africa & Global — cards, mobile money, bank transfer",
        PaymentGatewayType.Stripe       => "Global — cards, wallets, bank debit",
        PaymentGatewayType.Korapay      => "Nigeria & Africa — cards, virtual accounts",
        PaymentGatewayType.Monnify      => "Nigeria — bank transfer, USSD, card",
        PaymentGatewayType.Squad        => "Nigeria (GTCo) — cards, transfers",
        PaymentGatewayType.Interswitch  => "Nigeria — Quickteller / Webpay",
        PaymentGatewayType.Remita       => "Nigeria — government & corporate payments",
        PaymentGatewayType.Opay         => "Nigeria & Africa — mobile money, cards",
        PaymentGatewayType.PeachPayments=> "South Africa, Kenya, Nigeria, Botswana — hosted checkout",
        PaymentGatewayType.DpoGroup     => "Africa (19+ countries) — cards, mobile money",
        PaymentGatewayType.PawaPay      => "Africa — mobile money (GHS, TZS, UGX, RWF, ZMW)",
        PaymentGatewayType.Checkout     => "Global — cards, digital wallets",
        PaymentGatewayType.BenefitPay   => "Bahrain — BenefitPay QR / mobile",
        PaymentGatewayType.Knet         => "Kuwait — KNET card network",
        _                               => string.Empty,
    };

    /// <summary>
    /// Returns whether the key fields for a gateway are populated — never the key values.
    /// </summary>
    private object KeyStatus(PaymentGatewayType type) => type switch
    {
        PaymentGatewayType.Paystack     => new { secretKey = HasValue(_config.Paystack.SecretKey), publicKey = HasValue(_config.Paystack.PublicKey) },
        PaymentGatewayType.Flutterwave  => new { secretKey = HasValue(_config.FlutterwaveConfig.SecretKey), publicKey = HasValue(_config.FlutterwaveConfig.PublicKey), encryptionKey = HasValue(_config.FlutterwaveConfig.EncryptionKey) },
        PaymentGatewayType.Stripe       => new { secretKey = HasValue(_config.Stripe.SecretKey) },
        PaymentGatewayType.Korapay      => new { secretKey = HasValue(_config.Korapay.SecretKey), publicKey = HasValue(_config.Korapay.PublicKey) },
        PaymentGatewayType.Monnify      => new { apiKey = HasValue(_config.Monnify.ApiKey), secretKey = HasValue(_config.Monnify.SecretKey), contractCode = HasValue(_config.Monnify.ContractCode) },
        PaymentGatewayType.Squad        => new { secretKey = HasValue(_config.Squad.SecretKey), publicKey = HasValue(_config.Squad.PublicKey) },
        PaymentGatewayType.PeachPayments=> new { entityId = HasValue(_config.PeachPayments.EntityId), accessToken = HasValue(_config.PeachPayments.AccessToken) },
        PaymentGatewayType.Checkout     => new { secretKey = HasValue(_config.Checkout.SecretKey), publicKey = HasValue(_config.Checkout.PublicKey) },
        PaymentGatewayType.Interswitch  => new { clientId = HasValue(_config.Interswitch.ClientId), clientSecret = HasValue(_config.Interswitch.ClientSecret) },
        PaymentGatewayType.Remita       => new { merchantId = HasValue(_config.Remita.MerchantId), apiKey = HasValue(_config.Remita.ApiKey) },
        PaymentGatewayType.Opay         => new { merchantId = HasValue(_config.Opay.MerchantId), secretKey = HasValue(_config.Opay.SecretKey) },
        PaymentGatewayType.DpoGroup     => new { companyToken = HasValue(_config.DpoGroup.CompanyToken) },
        PaymentGatewayType.PawaPay      => new { apiToken = HasValue(_config.PawaPay.ApiToken) },
        PaymentGatewayType.BenefitPay   => new { merchantId = HasValue(_config.BenefitPay.MerchantId), apiKey = HasValue(_config.BenefitPay.ApiKey) },
        PaymentGatewayType.Knet         => new { transportId = HasValue(_config.Knet.TransportId) },
        _                               => new { },
    };

    private static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("YOUR_");
}
