using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentGateway
{
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

    /// <summary>
    /// Saves a payment method for future use
    /// </summary>
    /// <param name="request">Payment method details</param>
    /// <returns>Response containing saved payment method details</returns>
    Task<PaymentMethodResponse> SavePaymentMethodAsync(PaymentMethodRequest request);
}
