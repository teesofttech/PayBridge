using PayBridge.SDK.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Infrastructure.Gateways;
public class FlutterwaveGateway : IPaymentGateway
{
    public Task<PaymentStatusResponse> GetPaymentStatus(string paymentId)
    {
        throw new NotImplementedException();
    }

    public Task<PaymentResponse> ProcessPayment(PaymentRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<RefundResponse> RefundPayment(string paymentId, decimal amount)
    {
        throw new NotImplementedException();
    }
}
