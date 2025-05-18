using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Application.Dtos.Response;
using PayBridge.SDK.Application.Exceptions;
using PayBridge.SDK.Application.Interfaces;
using PayBridge.SDK.Domain;
using PayBridge.SDK.Domain.Entities;
using PayBridge.SDK.Domain.Enums;
using System.Text.Json;

namespace PayBridge.SDK.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly Dictionary<PaymentGatewayType, IPaymentGateway> _gateways;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentGatewayConfig _config;
   

    public PaymentService(
        ITransactionRepository transactionRepository,
         IEnumerable<IPaymentGateway> gateways,  // Inject all gateways directly
        ILogger<PaymentService> logger,
        PaymentGatewayConfig config)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _gateways = gateways.ToDictionary(g => g.GatewayType);
      
    }

    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request, PaymentGatewayType gateway = PaymentGatewayType.Automatic)
    {
        ValidateRequest(request);

        // Select gateway or use specified
        PaymentGatewayType selectedGateway = gateway != PaymentGatewayType.Automatic
            ? gateway
            : SelectBestGateway(request);

        _logger.LogInformation("Creating payment of {Amount} {Currency} using {Gateway} gateway",
            request.Amount, request.Currency, selectedGateway);

        if (!_gateways.ContainsKey(selectedGateway))
        {
            _logger.LogError("Gateway {Gateway} is not configured", selectedGateway);
            throw new PaymentGatewayException($"Gateway {selectedGateway} is not configured");
        }

        try
        {
            var response = await _gateways[selectedGateway].CreatePaymentAsync(request);

            _logger.LogInformation("Payment creation {Status}: {Reference}",
                response.Success ? "successful" : "failed", response.TransactionReference);

            if (response.Success)
            {
                // Save transaction to repository
                await _transactionRepository.CreateAsync(new PaymentTransaction
                {
                    TransactionReference = response.TransactionReference,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    CustomerEmail = request.CustomerEmail,
                    CustomerName = request.CustomerName,
                    Status = response.Status,
                    Gateway = selectedGateway,
                    GatewayResponse = JsonSerializer.Serialize(response.GatewayResponse),
                    CreatedAt = DateTime.UtcNow                    
                });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed with {Gateway}", selectedGateway);
            throw new PaymentGatewayException($"Payment processing failed with {selectedGateway}", ex);
        }
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(string transactionReference, PaymentGatewayType gateway = PaymentGatewayType.Automatic)
    {
        if (string.IsNullOrEmpty(transactionReference))
        {
            throw new ArgumentException("Transaction reference is required", nameof(transactionReference));
        }

        _logger.LogInformation("Verifying payment: {Reference}", transactionReference);

        // If automatic, try to determine gateway from reference or database
        PaymentGatewayType selectedGateway = gateway;
        if (gateway == PaymentGatewayType.Automatic)
        {
            // First try to find in the database
            var transaction = await _transactionRepository.GetByReferenceAsync(transactionReference);
            if (transaction != null)
            {
                selectedGateway = transaction.Gateway;
                _logger.LogInformation("Found transaction in database, using {Gateway} gateway", selectedGateway);
            }
            else
            {
                // Try to determine from reference prefix
                selectedGateway = DetermineGatewayFromReference(transactionReference);
                _logger.LogInformation("Determined gateway from reference: {Gateway}", selectedGateway);
            }
        }

        if (!_gateways.ContainsKey(selectedGateway))
        {
            _logger.LogError("Gateway {Gateway} is not configured", selectedGateway);
            throw new PaymentGatewayException($"Gateway {selectedGateway} is not configured");
        }

        try
        {
            var response = await _gateways[selectedGateway].VerifyPaymentAsync(transactionReference);

            _logger.LogInformation("Payment verification {Status}: {Reference}, Payment Status: {PaymentStatus}",
                response.Success ? "successful" : "failed", transactionReference, response.Status);

            if (response.Success)
            {
                // Update transaction in repository if it exists
                var transaction = await _transactionRepository.GetByReferenceAsync(transactionReference);
                if (transaction != null)
                {
                    transaction.Status = response.Status;
                    transaction.CompletedAt = response.Status == PaymentStatus.Successful ? response.PaymentDate : null;
                    await _transactionRepository.UpdateAsync(transaction);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment verification failed with {Gateway}", selectedGateway);
            throw new PaymentGatewayException($"Payment verification failed with {selectedGateway}", ex);
        }
    }

    public async Task<RefundResponse> RefundPaymentAsync(RefundRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrEmpty(request.TransactionReference))
        {
            throw new ArgumentException("Transaction reference is required", nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Refund amount must be greater than zero", nameof(request));
        }

        _logger.LogInformation("Processing refund of {Amount} for transaction: {Reference}",
            request.Amount, request.TransactionReference);

        // Find transaction in database to determine gateway
        var transaction = await _transactionRepository.GetByReferenceAsync(request.TransactionReference);
        if (transaction == null)
        {
            _logger.LogError("Transaction not found: {Reference}", request.TransactionReference);
            throw new PaymentGatewayException($"Transaction not found: {request.TransactionReference}");
        }

        // Verify transaction is in a refundable state
        if (transaction.Status != PaymentStatus.Successful)
        {
            _logger.LogError("Transaction not in refundable state: {Reference}, Status: {Status}",
                request.TransactionReference, transaction.Status);
            throw new PaymentGatewayException($"Transaction not in refundable state: {transaction.Status}");
        }

        // Verify refund amount does not exceed transaction amount
        if (request.Amount > transaction.Amount)
        {
            _logger.LogError("Refund amount exceeds transaction amount: {RefundAmount} > {TransactionAmount}",
                request.Amount, transaction.Amount);
            throw new PaymentGatewayException($"Refund amount exceeds transaction amount");
        }

        PaymentGatewayType selectedGateway = transaction.Gateway;
        if (!_gateways.ContainsKey(selectedGateway))
        {
            _logger.LogError("Gateway {Gateway} is not configured", selectedGateway);
            throw new PaymentGatewayException($"Gateway {selectedGateway} is not configured");
        }

        try
        {
            var response = await _gateways[selectedGateway].RefundPaymentAsync(request);

            _logger.LogInformation("Refund processing {Status}: {Reference}, Refund Reference: {RefundReference}",
                response.Success ? "successful" : "failed", request.TransactionReference, response.RefundReference);

            if (response.Success)
            {
                // Update transaction status if full refund
                if (request.Amount >= transaction.Amount)
                {
                    transaction.Status = PaymentStatus.Refunded;
                    await _transactionRepository.UpdateAsync(transaction);
                }

                // TODO: Save refund record to refund repository if implemented
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refund processing failed with {Gateway}", selectedGateway);
            throw new PaymentGatewayException($"Refund processing failed with {selectedGateway}", ex);
        }
    }

    public async Task<PaymentMethodResponse> SavePaymentMethodAsync(PaymentMethodRequest request, PaymentGatewayType gateway)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrEmpty(request.CustomerEmail))
        {
            throw new ArgumentException("Customer email is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Token))
        {
            throw new ArgumentException("Payment method token is required", nameof(request));
        }

        if (gateway == PaymentGatewayType.Automatic)
        {
            throw new ArgumentException("A specific gateway must be specified for saving payment methods", nameof(gateway));
        }

        _logger.LogInformation("Saving payment method for customer {Email} using {Gateway} gateway",
            request.CustomerEmail, gateway);

        if (!_gateways.ContainsKey(gateway))
        {
            _logger.LogError("Gateway {Gateway} is not configured", gateway);
            throw new PaymentGatewayException($"Gateway {gateway} is not configured");
        }

        try
        {
            var response = await _gateways[gateway].SavePaymentMethodAsync(request);

            _logger.LogInformation("Payment method saving {Status} for customer {Email}",
                response.Success ? "successful" : "failed", request.CustomerEmail);

            // TODO: Save payment method to repository if implemented

            return response;
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("Saving payment methods not supported by {Gateway}", gateway);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving payment method failed with {Gateway}", gateway);
            throw new PaymentGatewayException($"Saving payment method failed with {gateway}", ex);
        }
    }

    private PaymentGatewayType SelectBestGateway(PaymentRequest request)
    {
        // Logic to determine best gateway based on various factors

        // If only one gateway is enabled, use that
        if (_gateways.Count == 1)
        {
            return _gateways.Keys.First();
        }

        // Check for saved payment method - must use the same gateway
        if (!string.IsNullOrEmpty(request.SavedPaymentMethodId))
        {
            // TODO: Lookup saved payment method and return its gateway
            // For now, fall through to other selection logic
        }

        // Select based on currency
        switch (request.Currency?.ToUpper())
        {
            case "NGN":
                return ChooseAvailableGateway(PaymentGatewayType.Paystack, PaymentGatewayType.Flutterwave);

            case "KES":
            case "GHS":
            case "UGX":
            case "TZS":
            case "ZAR":
                return ChooseAvailableGateway(PaymentGatewayType.Flutterwave, PaymentGatewayType.Paystack);

            case "BHD":
                return ChooseAvailableGateway(PaymentGatewayType.BenefitPay);

            case "KWD":
                return ChooseAvailableGateway(PaymentGatewayType.Knet);

            case "USD":
            case "EUR":
            case "GBP":
            default:
                return ChooseAvailableGateway(PaymentGatewayType.Stripe, PaymentGatewayType.Checkout);
        }
    }

    private PaymentGatewayType ChooseAvailableGateway(params PaymentGatewayType[] preferredGateways)
    {
        // Try each gateway in order of preference
        foreach (var gateway in preferredGateways)
        {
            if (_gateways.ContainsKey(gateway))
            {
                return gateway;
            }
        }

        // Fall back to the first available gateway
        return _gateways.Keys.First();
    }

    private PaymentGatewayType DetermineGatewayFromReference(string transactionReference)
    {
        // Extract gateway from transaction reference prefix
        if (transactionReference.StartsWith("ST_"))
        {
            return PaymentGatewayType.Stripe;
        }

        if (transactionReference.StartsWith("PS_"))
        {
            return PaymentGatewayType.Paystack;
        }

        if (transactionReference.StartsWith("FW_"))
        {
            return PaymentGatewayType.Flutterwave;
        }

        if (transactionReference.StartsWith("CO_"))
        {
            return PaymentGatewayType.Checkout;
        }

        if (transactionReference.StartsWith("BP_"))
        {
            return PaymentGatewayType.BenefitPay;
        }

        if (transactionReference.StartsWith("KN_"))
        {
            return PaymentGatewayType.Knet;
        }

        // Default to configured default gateway
        _logger.LogWarning("Could not determine gateway from reference: {Reference}", transactionReference);
        return _config.DefaultGateway;
    }

    private void ValidateRequest(PaymentRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Currency))
        {
            throw new ArgumentException("Currency is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.CustomerEmail))
        {
            throw new ArgumentException("Customer email is required", nameof(request));
        }
    }
}
