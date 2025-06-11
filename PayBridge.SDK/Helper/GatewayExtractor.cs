using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Helper;
public static class GatewayExtractor
{

    /// <summary>
    /// Detects which payment gateway sent the webhook notification
    /// based on the structure of the webhook data
    /// </summary>
    public static PaymentGatewayType DetectGatewayFromWebhook(object webhookData)
    {
        // Convert to dynamic to inspect properties
        dynamic data = webhookData;

        try
        {
            // Paystack webhooks contain an 'event' property
            if (((IDictionary<string, object>)data).ContainsKey("event"))
            {
                return PaymentGatewayType.Paystack;
            }

            // Flutterwave webhooks contain a 'flw_ref' property
            if (((IDictionary<string, object>)data).ContainsKey("flw_ref"))
            {
                return PaymentGatewayType.Flutterwave;
            }

            // Stripe webhooks contain a 'type' property starting with 'stripe.'
            if (((IDictionary<string, object>)data).ContainsKey("type") &&
                data.type.ToString().StartsWith("stripe."))
            {
                return PaymentGatewayType.Stripe;
            }

            // Checkout.com webhooks contain a '_links' property
            if (((IDictionary<string, object>)data).ContainsKey("_links"))
            {
                return PaymentGatewayType.Checkout;
            }

            // Default to Automatic if we can't determine the gateway
            return PaymentGatewayType.Automatic;
        }
        catch
        {
            // If we encounter any errors, default to Automatic
            return PaymentGatewayType.Automatic;
        }
    }

    /// <summary>
    /// Extracts the transaction reference from the webhook notification
    /// based on the gateway that sent it
    /// </summary>
    public static string ExtractReferenceFromWebhook(object webhookData, PaymentGatewayType gateway)
    {
        // Convert to dynamic to inspect properties
        dynamic data = webhookData;

        try
        {
            switch (gateway)
            {
                case PaymentGatewayType.Paystack:
                    // Paystack webhook data is nested in a 'data' property
                    return data.data.reference;

                case PaymentGatewayType.Flutterwave:
                    // Flutterwave uses 'tx_ref' for the merchant reference
                    return data.tx_ref;

                case PaymentGatewayType.Stripe:
                    // Stripe's webhook data depends on the event type
                    if (data.type.ToString().StartsWith("payment_intent."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    // For charge events
                    else if (data.type.ToString().StartsWith("charge."))
                    {
                        return data.data.@object.metadata.reference;
                    }
                    break;

                case PaymentGatewayType.Checkout:
                    // Checkout.com webhook format
                    return data.data.reference;
            }

            // If we couldn't extract a reference using the known formats,
            // try some common property names
            var dict = (IDictionary<string, object>)data;

            foreach (var prop in new[] { "reference", "transaction_reference", "txn_ref", "id" })
            {
                if (dict.ContainsKey(prop))
                {
                    return dict[prop].ToString();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

}
