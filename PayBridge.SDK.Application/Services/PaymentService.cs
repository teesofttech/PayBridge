using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Interfaces;

namespace PayBridge.SDK.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;

    public PaymentService(ITransactionRepository transactionRepository, IPaymentGateway paymentGateway)
    {
        _transactionRepository = transactionRepository;
        _paymentGateway = paymentGateway;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        // Step 1: Send request to payment gateway and get a response.
        var paymentResponse = await _paymentGateway.ProcessPaymentAsync(paymentRequest);

        // Step 2: Save transaction record to DB if successful
        var transaction = new Domain.Entities.TransactionRecord
        {
            // TransactionUniqueId = paymentRequest.TransactionId,
            Amount = paymentRequest.Amount,
            Currency = paymentRequest.Currency,
            // Status = paymentResponse.Status,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.SaveTransactionAsync(transaction);

        return paymentResponse;
    }

    public async Task<PaymentStatusResponse> GetPaymentStatusAsync(string transactionId)
    {
        // Get the status of the transaction from the payment gateway.
        return await _paymentGateway.GetPaymentStatusAsync(transactionId);
    }

    public async Task<RefundResponse> ProcessRefundAsync(string transactionId, decimal amount)
    {
        // Step 1: Call the payment gateway to initiate refund
        var refundResponse = await _paymentGateway.RefundPaymentAsync(transactionId, amount);

        // Step 2: Record the refund transaction if successful
        var transaction = new Domain.Entities.TransactionRecord
        {
            TransactionUniqueId = refundResponse.RefundTransactionId,
            Amount = refundResponse.RefundAmount,
            Status = refundResponse.Status,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.SaveTransactionAsync(transaction);

        return refundResponse;
    }

}
