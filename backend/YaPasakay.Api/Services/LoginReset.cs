using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class LoginReset
{
    public static async Task<ResetPasswordResult> ResetAsync(
        AppDbContext db,
        IOtpStore otpStore,
        AppUser user,
        CancellationToken cancellationToken)
    {
        await RevokeSessionsAsync(db, user.Id, cancellationToken);
        otpStore.Save(user.PhoneNumber, FixedOtpSender.DevCode, TimeSpan.FromMinutes(10));

        return new ResetPasswordResult(
            user.PhoneNumber,
            FixedOtpSender.DevCode,
            $"Password reset. {user.FullName} can sign in with OTP {FixedOtpSender.DevCode}. Other sessions were signed out.");
    }

    public static async Task<(ResetPasswordResult? Result, string? Error)> SetPasswordAsync(
        AppDbContext db,
        AppUser user,
        string? password,
        CancellationToken cancellationToken)
    {
        if (!SecretHasher.IsStrongPassword(password ?? string.Empty))
        {
            return (null, "Password must be at least 6 characters.");
        }

        await RevokeSessionsAsync(db, user.Id, cancellationToken);
        user.PasswordHash = SecretHasher.Hash(password!.Trim());
        user.UpdatedAtUtc = DateTime.UtcNow;

        return (
            new ResetPasswordResult(
                user.PhoneNumber,
                string.Empty,
                $"{user.FullName} can now sign in with this password. Other sessions were signed out."),
            null);
    }

    private static async Task RevokeSessionsAsync(AppDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(x => x.AppUserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }
    }
}
