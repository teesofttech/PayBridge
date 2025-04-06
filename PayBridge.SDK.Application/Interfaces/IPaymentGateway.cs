using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentGateway
{
    Task<PaymentResponse> ProcessPayment(PaymentRequest request);
    Task<PaymentStatusResponse> GetPaymentStatus(string paymentId);
    Task<RefundResponse> RefundPayment(string paymentId, decimal amount);
}
