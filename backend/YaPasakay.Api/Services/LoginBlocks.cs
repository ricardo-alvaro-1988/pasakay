using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Common;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class LoginBlocks
{
    public const string Message = "This email or mobile number is blocked and cannot sign in.";

    public static string? NormalizeEmail(string? email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        return value.Contains('@') ? value : null;
    }

    public static string? NormalizePhone(string? phone) =>
        PhoneNormalizer.TryNormalizePhMobile(phone, out var normalized, out _) ? normalized : null;

    public static async Task<bool> IsBlockedAsync(
        AppDbContext db,
        string? email,
        string? phone,
        CancellationToken cancellationToken)
    {
        var emailKey = NormalizeEmail(email);
        var phoneKey = NormalizePhone(phone);
        if (emailKey is null && phoneKey is null)
        {
            return false;
        }

        var query = db.CustomerLoginBlocks.AsNoTracking().Where(x => x.LiftedAtUtc == null);
        if (emailKey is not null && phoneKey is not null)
        {
            return await query.AnyAsync(x => x.Email == emailKey || x.PhoneNumber == phoneKey, cancellationToken);
        }

        if (emailKey is not null)
        {
            return await query.AnyAsync(x => x.Email == emailKey, cancellationToken);
        }

        return await query.AnyAsync(x => x.PhoneNumber == phoneKey, cancellationToken);
    }
}
