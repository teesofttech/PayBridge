using System.Collections.Concurrent;
using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Example.Services;

/// <summary>
/// Lightweight in-memory order store used by the example project.
///
/// In a real application this would be replaced by an EF Core repository or
/// any other persistence layer. The point here is to show what business logic
/// you'd typically trigger after a payment is confirmed.
/// </summary>
public class OrderService
{
    // ── Internal record ───────────────────────────────────────────────────────

    public record Order(
        string  OrderId,
        string  CustomerEmail,
        decimal Amount,
        string  Currency,
        string  Description)
    {
        public PaymentStatus Status          { get; set; } = PaymentStatus.Pending;
        public string        TransactionRef  { get; set; } = string.Empty;
        public DateTime      PaidAt          { get; set; }
    }

    // ── Storage ───────────────────────────────────────────────────────────────

    private readonly ConcurrentDictionary<string, Order> _orders = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new pending order and returns its generated ID.
    /// Call this before initiating the payment so you can include the orderId
    /// in <c>Metadata</c> and tie the payment back to the order in your webhook.
    /// </summary>
    public Order Create(
        string  customerEmail,
        decimal amount,
        string  currency,
        string  description)
    {
        var order = new Order(
            OrderId:       Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            CustomerEmail: customerEmail,
            Amount:        amount,
            Currency:      currency,
            Description:   description);

        _orders[order.OrderId] = order;
        return order;
    }

    /// <summary>Retrieves an order by its internal ID.</summary>
    public Order? GetById(string orderId) =>
        _orders.TryGetValue(orderId, out var o) ? o : null;

    /// <summary>Retrieves an order by its gateway transaction reference.</summary>
    public Order? GetByTransactionRef(string transactionRef) =>
        _orders.Values.FirstOrDefault(o => o.TransactionRef == transactionRef);

    /// <summary>
    /// Marks an order as paid. Call this from your webhook / redirect handler
    /// after <c>IPaymentService.VerifyPaymentAsync</c> returns success.
    /// </summary>
    public bool MarkAsPaid(string orderId, string transactionRef)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;

        order.Status         = PaymentStatus.Successful;
        order.TransactionRef = transactionRef;
        order.PaidAt         = DateTime.UtcNow;
        return true;
    }

    /// <summary>Marks an order as failed.</summary>
    public bool MarkAsFailed(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        order.Status = PaymentStatus.Failed;
        return true;
    }

    /// <summary>Marks an order as refunded.</summary>
    public bool MarkAsRefunded(string orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) return false;
        order.Status = PaymentStatus.Refunded;
        return true;
    }

    /// <summary>All orders — useful for the /api/orders debug endpoint.</summary>
    public IReadOnlyList<Order> All() => _orders.Values.ToList();
}
