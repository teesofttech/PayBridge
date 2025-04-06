using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Request;
public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerName { get; set; }
    public string PaymentMethod { get; set; }
}
