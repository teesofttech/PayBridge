using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Domain.Enums;
public enum PaymentGatewayType
{
    Automatic = 0,
    Flutterwave = 1,
    Paystack = 2,
    Stripe = 3,
    Checkout = 4,
    BenefitPay = 5,
    Knet = 6
}