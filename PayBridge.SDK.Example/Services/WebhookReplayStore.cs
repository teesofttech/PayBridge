using System.Collections.Concurrent;
using System.Security.Cryptography;
using PayBridge.SDK.Enums;

namespace PayBridge.SDK.Example.Services;

public interface IWebhookReplayStore
{
    bool TryStart(PaymentGatewayType gateway, ReadOnlySpan<byte> payload, out string receiptId);
    void Complete(string receiptId);
    void Abandon(string receiptId);
}

public sealed class WebhookReplayStore : IWebhookReplayStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private const int MaximumReceipts = 10_000;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _receipts = new();

    public bool TryStart(
        PaymentGatewayType gateway,
        ReadOnlySpan<byte> payload,
        out string receiptId)
    {
        RemoveExpiredReceipts();
        var gatewayBytes = BitConverter.GetBytes((int)gateway);
        var input = new byte[gatewayBytes.Length + payload.Length];
        gatewayBytes.CopyTo(input, 0);
        payload.CopyTo(input.AsSpan(gatewayBytes.Length));
        receiptId = Convert.ToHexString(SHA256.HashData(input));
        return _receipts.TryAdd(receiptId, DateTimeOffset.UtcNow);
    }

    public void Complete(string receiptId) =>
        _receipts[receiptId] = DateTimeOffset.UtcNow;

    public void Abandon(string receiptId) => _receipts.TryRemove(receiptId, out _);

    private void RemoveExpiredReceipts()
    {
        var cutoff = DateTimeOffset.UtcNow - Retention;
        foreach (var receipt in _receipts.Where(item => item.Value < cutoff))
        {
            _receipts.TryRemove(receipt.Key, out _);
        }

        if (_receipts.Count < MaximumReceipts)
        {
            return;
        }

        foreach (var receipt in _receipts
                     .OrderBy(item => item.Value)
                     .Take(_receipts.Count - MaximumReceipts + 1))
        {
            _receipts.TryRemove(receipt.Key, out _);
        }
    }
}
