using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Dtos.Response;
public class RefundResponseDto
{
    public string RefundTransactionId { get; set; }
    public decimal RefundAmount { get; set; }
    public string Status { get; set; }
    public string GatewayResponse { get; set; }
}
