using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/alerts")]
public class AdminAlertsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminAlertsSummary>> Summary(CancellationToken cancellationToken)
    {
        var openSos = await db.SupportTickets.CountAsync(
            x => x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var unread = await db.AdminNotifications.CountAsync(
            x => x.Kind == NotificationKind.Sos && x.ReadAtUtc == null,
            cancellationToken);
        var pendingBilling = await db.Operators.CountAsync(
            op => op.IsActive && db.Trips.Any(t =>
                t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null),
            cancellationToken);
        var pendingAccountDeletes = await db.CustomerProfiles.CountAsync(
            x => x.DeleteStatus == DeleteAccountStatus.Pending,
            cancellationToken);
        return Ok(new AdminAlertsSummary(openSos, unread, pendingBilling, pendingAccountDeletes));
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<AdminAlertItem>>> Inbox(CancellationToken cancellationToken)
    {
        var rows = await db.AdminNotifications
            .Where(x => x.Kind == NotificationKind.Sos)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.Kind,
                x.Title,
                x.Body,
                x.SupportTicketId,
                x.CreatedAtUtc,
                x.ReadAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => new AdminAlertItem(
            x.Id,
            x.Kind,
            x.Title,
            x.Body,
            x.SupportTicketId,
            DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
            x.ReadAtUtc is DateTime read ? DateTime.SpecifyKind(read, DateTimeKind.Utc) : null)).ToList());
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<AdminAlertItem>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.AdminNotifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        row.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new AdminAlertItem(
            row.Id,
            row.Kind,
            row.Title,
            row.Body,
            row.SupportTicketId,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(row.ReadAtUtc.Value, DateTimeKind.Utc)));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.AdminNotifications
            .Where(x => x.Kind == NotificationKind.Sos && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now), cancellationToken);
        return Ok(new { message = "SOS alerts marked read." });
    }
}
