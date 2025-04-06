using PayBridge.SDK.Application.Dtos.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentGateway
{
    Task<PaymentResponse> ProcessPayment(PaymentRequest request);
    Task<PaymentStatusResponse> GetPaymentStatus(string paymentId);
    Task<RefundResponse> RefundPayment(string paymentId, decimal amount);
}
