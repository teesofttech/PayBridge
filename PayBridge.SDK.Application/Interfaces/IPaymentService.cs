using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentService
{
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest paymentRequest);
    Task<PaymentStatusResponse> GetPaymentStatusAsync(string transactionId);
    Task<RefundResponse> ProcessRefundAsync(string transactionId, decimal amount);
}
