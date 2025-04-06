using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Interfaces;

namespace PayBridge.SDK.Infrastructure.Gateways;
public class FlutterwaveGateway : IPaymentGateway
{
    public Task<PaymentStatusResponse> GetPaymentStatusAsync(string paymentId)
    {
        throw new NotImplementedException();
    }

    public Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<RefundResponse> RefundPaymentAsync(string paymentId, decimal amount)
    {
        throw new NotImplementedException();
    }
}
