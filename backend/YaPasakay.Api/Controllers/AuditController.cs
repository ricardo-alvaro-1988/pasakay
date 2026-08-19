using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit")]
public class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogItem>>> List(
        [FromQuery] string? q,
        [FromQuery] AuditAction? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.AuditLogs.AsNoTracking();
        if (action is not null)
        {
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Summary.Contains(term) ||
                x.Operator.CompanyName.Contains(term) ||
                (x.Actor != null && x.Actor.FullName.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Action,
                x.Summary,
                x.OperatorId,
                OperatorName = x.Operator.CompanyName,
                x.ActorUserId,
                ActorName = x.Actor != null ? x.Actor.FullName : null,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new AuditLogItem(
            x.Id,
            x.Action,
            Label(x.Action),
            x.Summary,
            x.OperatorId,
            x.OperatorName,
            x.ActorUserId,
            string.IsNullOrWhiteSpace(x.ActorName) ? "System" : x.ActorName,
            DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc))).ToList();

        return Ok(new PagedResult<AuditLogItem>(items, page, pageSize, total));
    }

    private static string Label(AuditAction action) => action switch
    {
        AuditAction.OperatorCreated => "Created",
        AuditAction.OperatorUpdated => "Updated",
        AuditAction.OperatorActivated => "Activated",
        AuditAction.OperatorDeactivated => "Deactivated",
        AuditAction.BillIssued => "Billed",
        _ => action.ToString()
    };
}
