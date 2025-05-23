using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Interfaces;
public interface IPaymentGateway
{
    PaymentGatewayType GatewayType { get; }

    /// <summary>
    /// Creates a payment using the gateway provider
    /// </summary>
    /// <param name="request">Payment request details</param>
    /// <returns>Response containing payment status and checkout URL</returns>
    Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request);

    /// <summary>
    /// Verifies the status of a payment
    /// </summary>
    /// <param name="transactionReference">The transaction reference to verify</param>
    /// <returns>Response containing verification details</returns>
    Task<VerificationResponse> VerifyPaymentAsync(string transactionReference);

    /// <summary>
    /// Process a refund for a successful payment
    /// </summary>
    /// <param name="request">Refund request details</param>
    /// <returns>Response containing refund status</returns>
    Task<RefundResponse> RefundPaymentAsync(RefundRequest request);
}
