using System.Collections.Concurrent;
using System.Security.Cryptography;
using YaPasakay.Application.Auth;

namespace YaPasakay.Api.Services;

public static class MobileAuthTicketStore
{
    private static readonly ConcurrentDictionary<string, Entry> Store = new(StringComparer.Ordinal);

    private sealed record Entry(AuthResponse Auth, DateTime ExpiresAtUtc);

    public static string Issue(AuthResponse auth, TimeSpan? lifetime = null)
    {
        Cleanup();
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Store[ticket] = new Entry(auth, DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)));
        return ticket;
    }

    public static AuthResponse? Take(string? ticket)
    {
        Cleanup();
        var key = (ticket ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0 || !Store.TryRemove(key, out var entry))
        {
            return null;
        }

        return entry.ExpiresAtUtc < DateTime.UtcNow ? null : entry.Auth;
    }

    private static void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in Store)
        {
            if (pair.Value.ExpiresAtUtc < now)
            {
                Store.TryRemove(pair.Key, out _);
            }
        }
    }
}
