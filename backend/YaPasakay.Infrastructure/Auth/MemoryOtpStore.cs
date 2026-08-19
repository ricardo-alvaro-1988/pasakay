using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class MemoryOtpStore(IOptions<OtpOptions> options) : IOtpStore
{
    private readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresAtUtc)> _codes = new();

    public void Save(string phone, string code, TimeSpan lifetime)
    {
        _codes[phone] = (code, DateTime.UtcNow.Add(lifetime));
    }

    public bool TryValidate(string phone, string code)
    {
        var trimmed = code.Trim();
        if (options.Value.AllowDevBypass && trimmed == FixedOtpSender.DevCode)
        {
            _codes.TryRemove(phone, out _);
            return true;
        }

        if (!_codes.TryGetValue(phone, out var entry))
        {
            return false;
        }

        if (DateTime.UtcNow > entry.ExpiresAtUtc)
        {
            _codes.TryRemove(phone, out _);
            return false;
        }

        var ok = string.Equals(entry.Code, trimmed, StringComparison.Ordinal);
        if (ok)
        {
            _codes.TryRemove(phone, out _);
        }

        return ok;
    }
}
