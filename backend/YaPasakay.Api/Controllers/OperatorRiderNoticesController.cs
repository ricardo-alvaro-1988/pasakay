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
public class OperatorRiderNoticesController(AppDbContext db) : ControllerBase
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
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.NotifyMinuteOfDay)
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

        if (!RiderNoticeService.TryParseMinute(request.NotifyAt, out var minuteOfDay))
        {
            return BadRequest(new { message = "Choose a daily time, such as 3:00 PM. Use Philippine time." });
        }

        var item = new RiderNotice
        {
            OperatorId = op.Id,
            Title = title,
            Body = body,
            NotifyMinuteOfDay = minuteOfDay,
            ScheduledAtUtc = RiderNoticeService.NextFireUtc(minuteOfDay, DateTime.UtcNow),
            IsActive = true
        };
        db.RiderNotices.Add(item);
        await db.SaveChangesAsync(cancellationToken);
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

        if (item.IsActive && item.CancelledAtUtc is null)
        {
            var now = DateTime.UtcNow;
            item.IsActive = false;
            item.CancelledAtUtc = now;
            item.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(Map(item));
    }

    private static RiderNoticeListItem Map(RiderNotice item)
    {
        var active = item.IsActive && item.CancelledAtUtc is null;
        return new RiderNoticeListItem(
            item.Id,
            item.Title,
            item.Body,
            RiderNoticeService.FormatMinute(item.NotifyMinuteOfDay),
            DateTime.SpecifyKind(item.ScheduledAtUtc, DateTimeKind.Utc),
            item.SentAtUtc is DateTime sent ? DateTime.SpecifyKind(sent, DateTimeKind.Utc) : null,
            active,
            active ? "Active" : "Stopped",
            DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc));
    }
}
