using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class OperatorContext
{
    public static async Task<(Operator? Operator, int Status, string? Message)> RequireAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(principal);
        if (userId is null)
        {
            return (null, 401, "Operator account not found or inactive.");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || user.Role != UserRole.Operator || !user.IsActive || user.OperatorId is null)
        {
            return (null, 401, "Operator account not found or inactive.");
        }

        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == user.OperatorId, cancellationToken);
        if (op is null || !op.IsActive)
        {
            return (null, 403, "This Operator is inactive. Ask Super Admin to activate the company.");
        }

        return (op, 200, null);
    }
}
