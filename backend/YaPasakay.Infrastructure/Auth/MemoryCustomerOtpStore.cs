using System.Collections.Concurrent;
using System.Security.Cryptography;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class MemoryCustomerOtpStore : ICustomerOtpStore
{
    private readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresAtUtc)> _codes = new();
    private readonly ConcurrentDictionary<string, DateTime> _sentAtUtc = new();
    private readonly ConcurrentDictionary<string, (string Phone, DateTime ExpiresAtUtc)> _registrations = new();

    public bool IsCoolingDown(string phone, TimeSpan cooldown)
    {
        return _sentAtUtc.TryGetValue(phone, out var sentAt) && DateTime.UtcNow - sentAt < cooldown;
    }

    public void Save(string phone, string code, TimeSpan lifetime)
    {
        _codes[phone] = (code, DateTime.UtcNow.Add(lifetime));
    }

    public void MarkSent(string phone)
    {
        _sentAtUtc[phone] = DateTime.UtcNow;
    }

    public bool TryValidate(string phone, string code)
    {
        var trimmed = code.Trim();
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

    public string IssueRegistration(string phone, TimeSpan lifetime)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _registrations[token] = (phone, DateTime.UtcNow.Add(lifetime));
        return token;
    }

    public bool TryReadRegistration(string token, out string phone)
    {
        phone = string.Empty;
        var key = (token ?? string.Empty).Trim();
        if (key.Length == 0 || !_registrations.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (DateTime.UtcNow > entry.ExpiresAtUtc)
        {
            _registrations.TryRemove(key, out _);
            return false;
        }

        phone = entry.Phone;
        return true;
    }

    public void ConsumeRegistration(string token)
    {
        var key = (token ?? string.Empty).Trim();
        if (key.Length > 0)
        {
            _registrations.TryRemove(key, out _);
        }
    }
}
