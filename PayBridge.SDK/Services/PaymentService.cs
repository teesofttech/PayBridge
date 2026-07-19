using Microsoft.Extensions.Logging;
using PayBridge.SDK.Application.Dtos;
using PayBridge.SDK.Application.Dtos.Request;
using PayBridge.SDK.Constants;
using PayBridge.SDK.Dtos.Request;
using PayBridge.SDK.Dtos.Response;
using PayBridge.SDK.Entities;
using PayBridge.SDK.Enums;
using PayBridge.SDK.Exceptions;
using PayBridge.SDK.Interfaces;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PayBridge.SDK;

public class PaymentService : IPaymentService
{
    private static readonly object IdempotencyLocksGate = new();
    private static readonly Dictionary<string, IdempotencyLock> IdempotencyLocks = [];
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRefundRepository _refundRepository;
    private readonly Dictionary<PaymentGatewayType, IPaymentGateway> _gateways;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentGatewayConfig _config;

    public PaymentService(
        ITransactionRepository transactionRepository,
        IRefundRepository refundRepository,
        IEnumerable<IPaymentGateway> gateways,  // Inject all gateways directly
        ILogger<PaymentService> logger,
        PaymentGatewayConfig config)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _refundRepository = refundRepository ?? throw new ArgumentNullException(nameof(refundRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _gateways = gateways.ToDictionary(g => g.GatewayType);

    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request, PaymentGatewayType gateway = PaymentGatewayType.Automatic)
    {
        ValidateRequest(request);
        request.Currency = NormalizeCurrency(request.Currency);

        if (request.PaymentMethodType == PaymentMethodType.Crypto)
        {
            throw new PaymentGatewayException(
                "No configured gateway supports payment method Crypto.");
        }

        if (_gateways.Count == 0)
        {
            throw new PaymentGatewayException("No payment gateways are configured");
        }

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

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return await CreateIdempotentPaymentAsync(request, selectedGateway);
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

    private async Task<PaymentResponse> CreateIdempotentPaymentAsync(
        PaymentRequest request,
        PaymentGatewayType selectedGateway)
    {
        if (request.IdempotencyKey!.Length > 255)
        {
            throw new ArgumentException("Idempotency key cannot exceed 255 characters.", nameof(request));
        }

        var key = request.IdempotencyKey;
        var fingerprint = CreateFingerprint(request, selectedGateway);
        var idempotencyLock = AcquireIdempotencyLock(key);
        await idempotencyLock.Semaphore.WaitAsync();
        try
        {
            var existing = await _transactionRepository.GetByIdempotencyKeyAsync(key);
            if (existing is not null)
            {
                return ReplayIdempotentPayment(existing, fingerprint);
            }

            PaymentTransaction transaction;
            try
            {
                transaction = await _transactionRepository.CreateAsync(new PaymentTransaction
                {
                    IdempotencyKey = key,
                    RequestFingerprint = fingerprint,
                    TransactionReference = $"IDEMPOTENCY-{Guid.NewGuid():N}",
                    Amount = request.Amount,
                    Currency = request.Currency,
                    CustomerEmail = request.CustomerEmail,
                    CustomerName = request.CustomerName,
                    Status = PaymentStatus.Pending,
                    Gateway = selectedGateway,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (DbUpdateException)
            {
                existing = await _transactionRepository.GetByIdempotencyKeyAsync(key);
                if (existing is null)
                {
                    throw;
                }

                return ReplayIdempotentPayment(existing, fingerprint);
            }

            try
            {
                var response = await _gateways[selectedGateway].CreatePaymentAsync(request);
                transaction.TransactionReference = string.IsNullOrWhiteSpace(response.TransactionReference)
                    ? transaction.TransactionReference
                    : response.TransactionReference;
                transaction.Status = response.Success ? response.Status : PaymentStatus.Failed;
                transaction.GatewayResponse = JsonSerializer.Serialize(response);
                await _transactionRepository.UpdateAsync(transaction);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Idempotent payment outcome is uncertain for {Gateway}", selectedGateway);
                throw new PaymentGatewayException(
                    $"Payment processing failed with {selectedGateway}; retry with the same idempotency key.", ex);
            }
        }
        finally
        {
            idempotencyLock.Semaphore.Release();
            ReleaseIdempotencyLock(key, idempotencyLock);
        }
    }

    private static string CreateFingerprint(
        PaymentRequest request,
        PaymentGatewayType gateway)
    {
        var value = JsonSerializer.Serialize(new
        {
            request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Email = request.CustomerEmail.Trim().ToUpperInvariant(),
            request.CustomerName,
            request.Description,
            request.RedirectUrl,
            request.WebhookUrl,
            request.PaymentMethodType,
            request.CustomerPhone,
            request.SavedPaymentMethodId,
            request.AppName,
            request.Logo,
            Metadata = request.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            Gateway = gateway
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static PaymentResponse ReplayIdempotentPayment(
        PaymentTransaction existing,
        string fingerprint)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(existing.RequestFingerprint),
                Encoding.UTF8.GetBytes(fingerprint)))
        {
            throw new PaymentGatewayException(
                "The idempotency key was already used with different payment parameters.");
        }

        if (!string.IsNullOrWhiteSpace(existing.GatewayResponse))
        {
            return JsonSerializer.Deserialize<PaymentResponse>(existing.GatewayResponse)
                ?? throw new PaymentGatewayException("The stored idempotent response is invalid.");
        }

        throw new PaymentGatewayException("The idempotent payment request is still processing.");
    }

    private static IdempotencyLock AcquireIdempotencyLock(string key)
    {
        lock (IdempotencyLocksGate)
        {
            if (!IdempotencyLocks.TryGetValue(key, out var value))
            {
                value = new IdempotencyLock();
                IdempotencyLocks.Add(key, value);
            }

            value.Users++;
            return value;
        }
    }

    private static void ReleaseIdempotencyLock(string key, IdempotencyLock value)
    {
        lock (IdempotencyLocksGate)
        {
            value.Users--;
            if (value.Users == 0)
            {
                IdempotencyLocks.Remove(key);
                value.Semaphore.Dispose();
            }
        }
    }

    private sealed class IdempotencyLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users { get; set; }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

        PaymentGatewayType selectedGateway = transaction.Gateway;
        if (!_gateways.ContainsKey(selectedGateway))
        {
            _logger.LogError("Gateway {Gateway} is not configured", selectedGateway);
            throw new PaymentGatewayException($"Gateway {selectedGateway} is not configured");
        }

        var fingerprint = ComputeRefundFingerprint(request);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingRefund = await _refundRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
            if (existingRefund is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(existingRefund.RequestFingerprint),
                        Encoding.UTF8.GetBytes(fingerprint)))
                {
                    throw new PaymentGatewayException(
                        "The idempotency key was already used with different refund parameters.");
                }

                if (!string.IsNullOrWhiteSpace(existingRefund.GatewayResponse))
                {
                    var storedResponse = JsonSerializer.Deserialize<RefundResponse>(existingRefund.GatewayResponse)
                        ?? throw new PaymentGatewayException("The stored refund response is invalid.");

                    return storedResponse;
                }

                return new RefundResponse
                {
                    Success = existingRefund.Status == PaymentStatus.Refunded,
                    TransactionReference = existingRefund.PaymentTransactionReference,
                    RefundReference = existingRefund.RefundReference,
                    Amount = existingRefund.Amount,
                    Status = existingRefund.Status,
                    Message = existingRefund.Status == PaymentStatus.Pending
                        ? "Refund is still processing"
                        : "Refund already recorded",
                    RefundDate = existingRefund.ProcessedAt ?? existingRefund.CreatedAt
                };
            }
        }

        var refund = new RefundTransaction
        {
            Id = Guid.NewGuid().ToString("N"),
            PaymentTransactionReference = request.TransactionReference,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            Amount = request.Amount,
            Currency = transaction.Currency,
            Reason = request.Reason,
            Status = PaymentStatus.Pending,
            Gateway = selectedGateway,
            CreatedAt = DateTime.UtcNow
        };
        refund.RefundReference = refund.Id;

        if (!await _refundRepository.TryReserveAsync(refund, transaction.Amount))
        {
            _logger.LogWarning(
                "Refund amount exceeds refundable balance for {Reference}",
                request.TransactionReference);
            throw new PaymentGatewayException("Refund amount exceeds refundable balance");
        }

        RefundResponse response;
        try
        {
            response = await _gateways[selectedGateway].RefundPaymentAsync(request);
        }
        catch (Exception ex)
        {
            await _refundRepository.FinalizeAsync(refund, new RefundResponse
            {
                Success = false,
                TransactionReference = request.TransactionReference,
                RefundReference = refund.RefundReference,
                Amount = request.Amount,
                Status = PaymentStatus.Failed,
                Message = ex.Message,
                RefundDate = DateTime.UtcNow
            });
            _logger.LogError(ex, "Refund processing failed with {Gateway}", selectedGateway);
            throw new PaymentGatewayException($"Refund processing failed with {selectedGateway}", ex);
        }

        _logger.LogInformation("Refund processing {Status}: {Reference}, Refund Reference: {RefundReference}",
            response.Success ? "successful" : "failed", request.TransactionReference, response.RefundReference);

        var finalizedRefund = await _refundRepository.FinalizeAsync(refund, response);

        return new RefundResponse
        {
            Success = finalizedRefund.Status == PaymentStatus.Refunded,
            RefundReference = finalizedRefund.RefundReference,
            TransactionReference = finalizedRefund.PaymentTransactionReference,
            Message = response.Message,
            Amount = finalizedRefund.Amount,
            Status = finalizedRefund.Status,
            RefundDate = finalizedRefund.ProcessedAt ?? finalizedRefund.CreatedAt
        };
    }

    private static string ComputeRefundFingerprint(RefundRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.TransactionReference,
            request.Amount,
            Reason = request.Reason ?? string.Empty
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private PaymentGatewayType SelectBestGateway(PaymentRequest request)
    {
        if (request.PaymentMethodType != PaymentMethodType.Card)
        {
            throw new PaymentGatewayException(
                $"Automatic routing for payment method {request.PaymentMethodType} " +
                "is not yet implemented. Specify a gateway explicitly.");
        }

        // Check for saved payment method - must use the same gateway
        if (!string.IsNullOrEmpty(request.SavedPaymentMethodId))
        {
            throw new PaymentGatewayException(
                "Saved payment method routing is not yet implemented. " +
                "Specify a gateway explicitly until provider binding is available.");
        }

        // Select based on currency
        var currency = NormalizeCurrency(request.Currency);
        var compatibleGateways = currency switch
        {
            "NGN" => new[] { PaymentGatewayType.Monnify, PaymentGatewayType.Squad, PaymentGatewayType.Korapay, PaymentGatewayType.Interswitch, PaymentGatewayType.Remita, PaymentGatewayType.Opay, PaymentGatewayType.Paystack, PaymentGatewayType.Flutterwave },
            "KES" or "GHS" or "UGX" or "TZS" or "ZAR" or "RWF" or "ZMW" or
                "CDF" or "XOF" or "XAF" or "MWK" =>
                [PaymentGatewayType.PeachPayments, PaymentGatewayType.PawaPay, PaymentGatewayType.DpoGroup, PaymentGatewayType.Flutterwave, PaymentGatewayType.Paystack],
            "BWP" => [PaymentGatewayType.PeachPayments, PaymentGatewayType.DpoGroup],
            "BHD" => [PaymentGatewayType.BenefitPay],
            "KWD" => [PaymentGatewayType.Knet],
            "USD" or "EUR" or "GBP" => [PaymentGatewayType.Stripe, PaymentGatewayType.Checkout],
            "JPY" => [PaymentGatewayType.Stripe],
            _ => []
        };

        return ChooseAvailableGateway(request, compatibleGateways);
    }

    #region [Private Methods]

    private PaymentGatewayType ChooseAvailableGateway(
        PaymentRequest request,
        IReadOnlyCollection<PaymentGatewayType> compatibleGateways)
    {
        if (_config.DefaultGateway != PaymentGatewayType.Automatic &&
            compatibleGateways.Contains(_config.DefaultGateway) &&
            _gateways.ContainsKey(_config.DefaultGateway))
        {
            _logger.LogInformation(
                "Selected configured default gateway {Gateway} for {Currency} and {PaymentMethod}",
                _config.DefaultGateway,
                request.Currency,
                request.PaymentMethodType);
            return _config.DefaultGateway;
        }

        // Try each gateway in order of preference
        foreach (var gateway in compatibleGateways)
        {
            if (_gateways.ContainsKey(gateway))
            {
                _logger.LogInformation(
                    "Selected compatible gateway {Gateway} for {Currency} and {PaymentMethod}",
                    gateway,
                    request.Currency,
                    request.PaymentMethodType);
                return gateway;
            }
        }

        throw new PaymentGatewayException(
            $"No configured gateway supports {NormalizeCurrency(request.Currency)} " +
            $"with payment method {request.PaymentMethodType}.");
    }

    private static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant();

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

        if (transactionReference.StartsWith("FW_") || transactionReference.StartsWith("FLW_"))
        {
            return PaymentGatewayType.Flutterwave;
        }

        if (transactionReference.StartsWith("MNF_"))
        {
            return PaymentGatewayType.Monnify;
        }

        if (transactionReference.StartsWith("SQ_"))
        {
            return PaymentGatewayType.Squad;
        }

        if (transactionReference.StartsWith("ISW_"))
        {
            return PaymentGatewayType.Interswitch;
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

        if (transactionReference.StartsWith(GatewayReferencePrefixes.Korapay))
        {
            return PaymentGatewayType.Korapay;
        }

        if (transactionReference.StartsWith("REM_"))
        {
            return PaymentGatewayType.Remita;
        }

        if (transactionReference.StartsWith("OP_"))
        {
            return PaymentGatewayType.Opay;
        }

        if (transactionReference.StartsWith("DPO_"))
        {
            return PaymentGatewayType.DpoGroup;
        }

        if (transactionReference.StartsWith("PP_"))
        {
            return PaymentGatewayType.PawaPay;
        }

        if (transactionReference.StartsWith("PEACH_"))
        {
            return PaymentGatewayType.PeachPayments;
        }
        throw new PaymentGatewayException(
            $"Unable to determine gateway from transaction reference '{transactionReference}'. " +
            "Specify a gateway explicitly for verification.");
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


    #endregion [Private Methods]
}
