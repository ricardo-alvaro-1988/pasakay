using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class CustomerContext
{
    public static async Task<(CustomerProfile? Customer, int Status, string? Message)> RequireAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(principal);
        if (userId is null)
        {
            return (null, 401, "Customer account not found or inactive.");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || user.Role != UserRole.Customer || !user.IsActive)
        {
            return (null, 401, "This login is for customers.");
        }

        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);
        if (customer is null)
        {
            return (null, 403, "Customer profile not found.");
        }

        if (customer.DeleteStatus == DeleteAccountStatus.Approved)
        {
            return (null, 403, "This account was closed.");
        }

        return (customer, 200, null);
    }
}
