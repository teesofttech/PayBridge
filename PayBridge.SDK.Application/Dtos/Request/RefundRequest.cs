using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Request;
public class RefundRequest
{
    public string TransactionReference { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; }
}