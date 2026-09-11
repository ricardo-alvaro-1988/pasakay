using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class OperatorAccess
{
    public static async Task<(AppUser? User, HashSet<string> Pages)> ResolveAsync(
        AppDbContext db,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken)
    {
        var id = AdminAccess.UserId(principal);
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
        if (user is null || user.Role != UserRole.Operator || !user.IsActive)
        {
            return [];
        }

        if (user.IsMainOperator)
        {
            var pages = OperatorAccessCatalog.PageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            pages.Add("roles");
            pages.Add("employees");
            return pages;
        }

        if (user.AccessGroup is null
            || user.AccessGroup.OperatorId is not Guid groupOp
            || user.OperatorId is not Guid userOp
            || groupOp != userOp)
        {
            return [];
        }

        return user.AccessGroup.Pages
            .Select(x => x.PageId)
            .Where(OperatorAccessCatalog.IsKnown)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static string? RequiredPage(string path)
    {
        var value = path.ToLowerInvariant();
        if (!value.StartsWith("/api/operator"))
        {
            return null;
        }

        if (value.StartsWith("/api/operator/access"))
        {
            return "employees";
        }

        if (value.StartsWith("/api/operator/bookings"))
        {
            return "bookings";
        }

        if (value.StartsWith("/api/operator/schedule"))
        {
            return "schedule";
        }

        if (value.StartsWith("/api/operator/riders")
            || value.StartsWith("/api/operator/rider-invite")
            || value.StartsWith("/api/operator/rider-applications"))
        {
            return "riders";
        }

        if (value.StartsWith("/api/operator/customers"))
        {
            return "customers";
        }

        if (value.StartsWith("/api/operator/wallet"))
        {
            return "wallet";
        }

        if (value.StartsWith("/api/operator/promos"))
        {
            return "promos";
        }

        if (value.Contains("/categories", StringComparison.Ordinal))
        {
            return "product-categories";
        }

        if (value.StartsWith("/api/operator/merchants"))
        {
            return "merchants";
        }

        if (value.StartsWith("/api/operator/reports/riders"))
        {
            return "rider-report";
        }

        if (value.StartsWith("/api/operator/reports/customers"))
        {
            return "customer-report";
        }

        if (value.StartsWith("/api/operator/reports/bookings"))
        {
            return "booking-report";
        }

        if (value.StartsWith("/api/operator/reports/commission"))
        {
            return "commission";
        }

        if (value.StartsWith("/api/operator/derive-fares"))
        {
            return "derive-fares";
        }

        if (value.StartsWith("/api/operator/fares"))
        {
            return value.Contains("/surcharge", StringComparison.Ordinal) ? "surcharges" : "fares";
        }

        if (value.StartsWith("/api/operator/pabili-matrix"))
        {
            return "pabili-matrix";
        }

        if (value.StartsWith("/api/operator/support"))
        {
            return "support";
        }

        if (value.StartsWith("/api/operator/inbox"))
        {
            return "inbox";
        }

        if (value.StartsWith("/api/operator/billing"))
        {
            return "billing";
        }

        if (value.StartsWith("/api/operator/fleet"))
        {
            return "fleet";
        }

        if (value.StartsWith("/api/operator/overview"))
        {
            return "overview";
        }

        if (value.StartsWith("/api/operator/company")
            || value.StartsWith("/api/operator/dispatch")
            || value.StartsWith("/api/operator/territories")
            || value.StartsWith("/api/operator/password"))
        {
            return "company";
        }

        if (value.StartsWith("/api/operator/alerts"))
        {
            return null;
        }

        // Desk root GET without a known suffix — allow any logged-in operator module.
        return null;
    }
}
