using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Domain.Enums;

namespace PayBridge.SDK.Infrastructure.Gateways;
internal abstract class BaseGateway : IPaymentGateway
{
    protected readonly PaymentGatewayConfig Config;
    protected readonly HttpClient HttpClient;
    protected readonly ILogger<BaseGateway> Logger;

    public PaymentGatewayType GatewayType => throw new NotImplementedException();

    protected BaseGateway(PaymentGatewayConfig config, ILogger<BaseGateway> logger)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        HttpClient = new HttpClient();
    }

    public abstract Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request);
    public abstract Task<VerificationResponse> VerifyPaymentAsync(string transactionReference);
    public abstract Task<RefundResponse> RefundPaymentAsync(RefundRequest request);

    protected string GenerateTransactionReference(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid().ToString("N")}";
    }
}
