using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Response;
public class PaymentStatusResponseDto
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public string GatewayResponse { get; set; }
}
