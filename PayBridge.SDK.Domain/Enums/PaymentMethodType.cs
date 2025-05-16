using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Domain.Enums;
public enum PaymentMethodType
{
    /// <summary>
    /// Credit or debit card
    /// </summary>
    Card = 0,

    /// <summary>
    /// Bank transfer
    /// </summary>
    BankTransfer = 1,

    /// <summary>
    /// Mobile money
    /// </summary>
    MobileMoney = 2,

    /// <summary>
    /// Digital wallet
    /// </summary>
    Wallet = 3,

    /// <summary>
    /// Cryptocurrency
    /// </summary>
    Crypto = 4,

    /// <summary>
    /// USSD transfer
    /// </summary>
    Ussd = 5,

    /// <summary>
    /// QR code payment
    /// </summary>
    QrCode = 6
}
