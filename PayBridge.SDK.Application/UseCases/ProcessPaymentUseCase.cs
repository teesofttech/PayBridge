using PayBridge.SDK.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Application.UseCases;
internal class ProcessPaymentUseCase
{
    private readonly IPaymentGateway _gateway;
    private readonly ITransactionRepository _transactionRepository;

    public ProcessPaymentUseCase(IPaymentGateway gateway, ITransactionRepository transactionRepository)
    {
        _gateway = gateway;
        _transactionRepository = transactionRepository;
    }

    public async Task<PaymentResponse> Execute(PaymentRequest request, string provider)
    {
        var response = await _gateway.ProcessPayment(request);

        // Log the transaction to DB
        var transaction = new TransactionRecord
        {
            Gateway = provider,
            PaymentId = response.PaymentId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = response.Status,
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepository.LogTransaction(transaction);
        return response;
    }
}
