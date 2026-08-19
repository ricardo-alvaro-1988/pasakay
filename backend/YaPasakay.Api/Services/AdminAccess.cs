using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class AdminAccess
{
    public static Guid? UserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static async Task<(AppUser? User, HashSet<string> Pages)> ResolveAsync(
        AppDbContext db,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken)
    {
        var id = UserId(principal);
        if (id is null)
        {
            return (null, []);
        }

        var user = await db.Users
            .AsNoTracking()
            .Include(x => x.AccessGroup)
            .ThenInclude(x => x!.Pages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return (user, PagesFor(user));
    }

    public static HashSet<string> PagesFor(AppUser? user)
    {
        if (user is null || user.Role != UserRole.Admin || !user.IsActive)
        {
            return [];
        }

        if (user.IsMainAdmin)
        {
            var pages = AccessCatalog.PageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            pages.Add("roles");
            pages.Add("admins");
            return pages;
        }

        return user.AccessGroup?.Pages
            .Select(x => x.PageId)
            .Where(AccessCatalog.IsKnown)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    public static async Task<MeResponse> ToMeAsync(AppDbContext db, AppUser user, CancellationToken cancellationToken)
    {
        var loaded = user.AccessGroup is not null || user.AccessGroupId is null
            ? user
            : await db.Users
                .AsNoTracking()
                .Include(x => x.AccessGroup)
                .ThenInclude(x => x!.Pages)
                .FirstAsync(x => x.Id == user.Id, cancellationToken);

        var pages = PagesFor(loaded).ToList();
        var groupName = loaded.IsMainAdmin
            ? "Administrator"
            : loaded.AccessGroup?.Name;
        string? companyName = null;
        if (loaded.OperatorId is Guid operatorId)
        {
            companyName = await db.Operators
                .AsNoTracking()
                .Where(x => x.Id == operatorId)
                .Select(x => x.CompanyName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new MeResponse(
            loaded.Id,
            loaded.PhoneNumber,
            loaded.FullName,
            loaded.Role,
            loaded.OperatorId,
            loaded.IsActive,
            loaded.IsMainAdmin,
            groupName,
            companyName,
            pages);
    }

    public static string? RequiredPage(string path)
    {
        var value = path.ToLowerInvariant();
        if (!value.StartsWith("/api/admin"))
        {
            return null;
        }

        if (value.StartsWith("/api/admin/access"))
        {
            return "settings";
        }

        if (value.StartsWith("/api/admin/billing"))
        {
            return "billing";
        }

        if (value.StartsWith("/api/admin/announcements"))
        {
            return "announcements";
        }

        if (value.StartsWith("/api/admin/support"))
        {
            return "support";
        }

        if (value.StartsWith("/api/admin/audit"))
        {
            return "audit";
        }

        if (value.StartsWith("/api/admin/territories"))
        {
            return "territories";
        }

        if (value.StartsWith("/api/admin/customers"))
        {
            return "customers";
        }

        if (value.StartsWith("/api/admin/fares"))
        {
            return "fares";
        }

        if (value.StartsWith("/api/admin/overview"))
        {
            return "overview";
        }

        if (value.StartsWith("/api/admin/operators"))
        {
            return "operators";
        }

        if (value.StartsWith("/api/admin/government-id-types"))
        {
            return "operators";
        }

        if (value.StartsWith("/api/admin/search"))
        {
            return "search";
        }

        return null;
    }
}
