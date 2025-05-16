using PayBridge.SDK.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Request;
public class VerificationRequest
{
    public string TransactionReference { get; set; }
    public PaymentGatewayType Gateway { get; set; } = PaymentGatewayType.Automatic;
}