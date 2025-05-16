using PayBridge.SDK.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Request;
public class PaymentMethodRequest
{
    public string CustomerEmail { get; set; }
    public string CustomerName { get; set; }
    public string Token { get; set; }
    public PaymentMethodType Type { get; set; }
    public bool IsDefault { get; set; }
}