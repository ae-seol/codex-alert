using System.Security.Cryptography;
using System.Text;

namespace CodexAlertRelay;

public sealed class DedupeCache
{
    private readonly Dictionary<string, DateTimeOffset> _seen = new();

    public bool ShouldSend(ToastPayload payload, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _seen.Where(pair => now - pair.Value > window).Select(pair => pair.Key).ToList();
        foreach (var key in expired)
        {
            _seen.Remove(key);
        }

        var dedupeKey = BuildKey(payload);
        if (_seen.TryGetValue(dedupeKey, out var lastSeen) && now - lastSeen <= window)
        {
            return false;
        }

        _seen[dedupeKey] = now;
        return true;
    }

    private static string BuildKey(ToastPayload payload)
    {
        var text = $"{payload.SourceAppId}|{payload.ToastId}|{payload.Title}|{payload.Body}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }
}
