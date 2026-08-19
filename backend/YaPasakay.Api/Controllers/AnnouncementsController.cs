using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/announcements")]
public class AnnouncementsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AnnouncementListItem>>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Announcements.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.Body.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AnnouncementListItem(
                x.Id,
                x.Title,
                x.Body,
                x.ForOperators,
                x.ForRiders,
                x.ForCustomers,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.IsActive,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AnnouncementListItem>(
            items.Select(MapUtc).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpPost]
    public async Task<ActionResult<AnnouncementListItem>> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        if (title.Length == 0 || body.Length == 0)
        {
            return BadRequest(new { message = "Title and body are required." });
        }

        if (title.Length > 120)
        {
            return BadRequest(new { message = "Title must be 120 characters or fewer." });
        }

        if (body.Length > 2000)
        {
            return BadRequest(new { message = "Body must be 2000 characters or fewer." });
        }

        if (!request.ForOperators && !request.ForRiders && !request.ForCustomers)
        {
            return BadRequest(new { message = "Choose at least one audience: Operators, riders, or customers." });
        }

        var start = ToUtc(request.StartsAtUtc);
        var end = ToUtc(request.EndsAtUtc);
        if (start is not null && end is not null && start > end)
        {
            return BadRequest(new { message = "Start must be before end. Use Philippine time." });
        }

        var item = new Announcement
        {
            Title = title,
            Body = body,
            ForOperators = request.ForOperators,
            ForRiders = request.ForRiders,
            ForCustomers = request.ForCustomers,
            StartsAtUtc = start,
            EndsAtUtc = end,
            IsActive = true
        };
        db.Announcements.Add(item);

        if (request.ForOperators)
        {
            var operatorIds = await db.Operators.Select(x => x.Id).ToListAsync(cancellationToken);
            var note = body.Length <= 400 ? body : body[..397] + "...";
            foreach (var operatorId in operatorIds)
            {
                db.OperatorNotifications.Add(new OperatorNotification
                {
                    OperatorId = operatorId,
                    Kind = NotificationKind.Announcement,
                    Title = title.Length <= 120 ? title : title[..117] + "...",
                    Body = note
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapUtc(ToItem(item)));
    }

    [HttpPost("{id:guid}/active")]
    public async Task<ActionResult<AnnouncementListItem>> SetActive(
        Guid id,
        [FromBody] SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapUtc(ToItem(item)));
    }

    private static AnnouncementListItem ToItem(Announcement x) =>
        new(
            x.Id,
            x.Title,
            x.Body,
            x.ForOperators,
            x.ForRiders,
            x.ForCustomers,
            x.StartsAtUtc,
            x.EndsAtUtc,
            x.IsActive,
            x.CreatedAtUtc);

    private static AnnouncementListItem MapUtc(AnnouncementListItem item) =>
        item with
        {
            StartsAtUtc = ToUtc(item.StartsAtUtc),
            EndsAtUtc = ToUtc(item.EndsAtUtc),
            CreatedAtUtc = DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc)
        };

    private static DateTime? ToUtc(DateTime? value) =>
        value is DateTime time ? DateTime.SpecifyKind(time.Kind == DateTimeKind.Unspecified ? time : time.ToUniversalTime(), DateTimeKind.Utc) : null;
}
