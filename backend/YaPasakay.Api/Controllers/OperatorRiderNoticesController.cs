using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/rider-notices")]
public class OperatorRiderNoticesController(AppDbContext db, RiderNoticeService notices) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RiderNoticeListItem>>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.RiderNotices.AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<RiderNoticeListItem>> Create(
        [FromBody] CreateRiderNoticeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var title = (request.Title ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        if (title.Length == 0 || body.Length == 0)
        {
            return BadRequest(new { message = "Title and message are required." });
        }

        if (title.Length > 80)
        {
            return BadRequest(new { message = "Title must be 80 characters or fewer." });
        }

        if (body.Length > 400)
        {
            return BadRequest(new { message = "Message must be 400 characters or fewer." });
        }

        if (request.ScheduledAtUtc is not DateTime scheduledRaw)
        {
            return BadRequest(new { message = "Choose when riders should be notified. Use Philippine time." });
        }

        var scheduled = ToUtc(scheduledRaw);
        var now = DateTime.UtcNow;
        if (scheduled < now.AddMinutes(-2))
        {
            return BadRequest(new { message = "Choose a time from now onward." });
        }

        if (scheduled > now.AddDays(30))
        {
            return BadRequest(new { message = "Schedule within the next 30 days." });
        }

        if (scheduled < now)
        {
            scheduled = now;
        }

        var item = new RiderNotice
        {
            OperatorId = op.Id,
            Title = title,
            Body = body,
            ScheduledAtUtc = scheduled
        };
        db.RiderNotices.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        if (scheduled <= DateTime.UtcNow)
        {
            await notices.DeliverAsync(item.Id, cancellationToken);
            item = await db.RiderNotices.AsNoTracking().FirstAsync(x => x.Id == item.Id, cancellationToken);
        }

        return Ok(Map(item));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RiderNoticeListItem>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var item = await db.RiderNotices.FirstOrDefaultAsync(
            x => x.Id == id && x.OperatorId == op.Id,
            cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (item.SentAtUtc is not null)
        {
            return BadRequest(new { message = "This announcement was already sent." });
        }

        if (item.CancelledAtUtc is null)
        {
            item.CancelledAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = item.CancelledAtUtc;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(Map(item));
    }

    private static RiderNoticeListItem Map(RiderNotice item)
    {
        var statusName = item.CancelledAtUtc is not null
            ? "Cancelled"
            : item.SentAtUtc is not null
                ? "Sent"
                : "Scheduled";
        return new RiderNoticeListItem(
            item.Id,
            item.Title,
            item.Body,
            DateTime.SpecifyKind(item.ScheduledAtUtc, DateTimeKind.Utc),
            item.SentAtUtc is DateTime sent ? DateTime.SpecifyKind(sent, DateTimeKind.Utc) : null,
            item.CancelledAtUtc is DateTime cancelled ? DateTime.SpecifyKind(cancelled, DateTimeKind.Utc) : null,
            statusName,
            DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc));
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
