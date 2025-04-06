using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentGateway
{
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
    Task<PaymentStatusResponse> GetPaymentStatusAsync(string paymentId);
    Task<RefundResponse> RefundPaymentAsync(string paymentId, decimal amount);
}
