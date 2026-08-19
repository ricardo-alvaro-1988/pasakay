using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class RiderContext
{
    public static async Task<(RiderProfile? Rider, int Status, string? Message)> RequireAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(principal);
        if (userId is null)
        {
            return (null, 401, "Rider account not found or inactive.");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || user.Role != UserRole.Rider || !user.IsActive)
        {
            return (null, 401, "Rider account not found or inactive.");
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);
        if (rider is null || !rider.IsActive)
        {
            return (null, 403, "Your rider profile is inactive. Contact your operator.");
        }

        return (rider, 200, null);
    }
}
