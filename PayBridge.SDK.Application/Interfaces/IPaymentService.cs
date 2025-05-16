using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Domain.Enums;

namespace PayBridge.SDK.Application.Interfaces;
public interface IPaymentService
{
    /// <summary>
    /// Creates a payment using the specified or automatically selected gateway
    /// </summary>
    /// <param name="request">Payment request details</param>
    /// <param name="gateway">Specific gateway to use or Automatic to let the service choose</param>
    /// <returns>Response containing payment status and checkout URL</returns>
    Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request, PaymentGatewayType gateway = PaymentGatewayType.Automatic);

    /// <summary>
    /// Verifies the status of a payment
    /// </summary>
    /// <param name="transactionReference">The transaction reference to verify</param>
    /// <param name="gateway">Specific gateway to use or Automatic to determine from the reference</param>
    /// <returns>Response containing verification details</returns>
    Task<VerificationResponse> VerifyPaymentAsync(string transactionReference, PaymentGatewayType gateway = PaymentGatewayType.Automatic);

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
    /// <param name="gateway">Specific gateway to use (cannot be Automatic)</param>
    /// <returns>Response containing saved payment method details</returns>
    Task<PaymentMethodResponse> SavePaymentMethodAsync(PaymentMethodRequest request, PaymentGatewayType gateway);
}
