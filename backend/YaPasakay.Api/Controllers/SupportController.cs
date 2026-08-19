using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/support")]
public class SupportController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SupportInboxResult>> List(
        [FromQuery] string? q,
        [FromQuery] SupportKind? kind,
        [FromQuery] SupportStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.SupportTickets.AsNoTracking();
        if (kind is not null)
        {
            query = query.Where(x => x.Kind == kind);
        }

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Subject.Contains(term) ||
                x.Body.Contains(term) ||
                x.Operator.CompanyName.Contains(term) ||
                x.Operator.AreaOfOperation.Contains(term) ||
                (x.Trip != null && x.Trip.Reference.Contains(term)) ||
                (x.Rider != null && x.Rider.AppUser.FullName.Contains(term)) ||
                (x.Customer != null && (x.Customer.FirstName.Contains(term) || x.Customer.LastName.Contains(term))));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Kind)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Kind,
                x.Status,
                x.OpenedBy,
                RiderName = x.Rider != null ? x.Rider.AppUser.FullName : null,
                RiderPhone = x.Rider != null ? x.Rider.AppUser.PhoneNumber : null,
                CustomerName = x.Customer != null
                    ? x.Customer.FirstName + " " + x.Customer.LastName
                    : x.Trip != null ? x.Trip.CustomerName : null,
                CustomerPhone = x.Customer != null
                    ? x.Customer.AppUser.PhoneNumber
                    : x.Trip != null ? x.Trip.CustomerPhone : null,
                x.Subject,
                x.Body,
                x.OperatorNotes,
                x.OperatorId,
                OperatorName = x.Operator.CompanyName,
                OperatorPhone = x.Operator.ContactPhone,
                Area = x.Operator.AreaOfOperation,
                Municipality = x.Trip != null && x.Trip.PickupBarangay != null
                    ? x.Trip.PickupBarangay.Municipality.Name
                    : x.Operator.AreaOfOperation,
                x.TripId,
                BookingNumber = x.Trip != null ? x.Trip.Reference : null,
                x.CreatedAtUtc,
                x.ClosedAtUtc
            })
            .ToListAsync(cancellationToken);

        var openSos = await db.SupportTickets.CountAsync(
            x => x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var openTickets = await db.SupportTickets.CountAsync(
            x => x.Status == SupportStatus.Open,
            cancellationToken);
        var closedTickets = await db.SupportTickets.CountAsync(
            x => x.Status == SupportStatus.Closed,
            cancellationToken);
        var unreadSosAlerts = await db.AdminNotifications.CountAsync(
            x => x.Kind == NotificationKind.Sos && x.ReadAtUtc == null,
            cancellationToken);

        var items = rows.Select(x =>
        {
            var name = x.OpenedBy == SupportOpenedBy.Rider
                ? x.RiderName ?? "Rider"
                : string.IsNullOrWhiteSpace(x.CustomerName) ? "Customer" : x.CustomerName.Trim();
            var phone = x.OpenedBy == SupportOpenedBy.Rider
                ? x.RiderPhone ?? ""
                : x.CustomerPhone ?? "";
            return new SupportTicketItem(
                x.Id,
                x.Kind,
                x.Status,
                x.OpenedBy,
                name,
                phone,
                x.Subject,
                x.Body,
                x.OperatorNotes,
                x.OperatorId,
                x.OperatorName,
                x.OperatorPhone,
                string.IsNullOrWhiteSpace(x.Municipality) ? x.Area : x.Municipality,
                x.TripId,
                x.BookingNumber,
                DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
                x.ClosedAtUtc is DateTime closed ? DateTime.SpecifyKind(closed, DateTimeKind.Utc) : null);
        }).ToList();

        return Ok(new SupportInboxResult(items, page, pageSize, total, openSos, openTickets, closedTickets, unreadSosAlerts));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await OperatorMaps.SupportDetailQuery(db)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return ticket is null ? NotFound() : Ok(OperatorMaps.SupportDetail(ticket));
    }
}
