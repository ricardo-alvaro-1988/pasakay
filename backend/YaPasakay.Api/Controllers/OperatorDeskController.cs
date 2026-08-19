using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator")]
public class OperatorDeskController(AppDbContext db) : ControllerBase
{
    [HttpGet("support")]
    public async Task<ActionResult<SupportInboxResult>> Support(
        [FromQuery] string? q,
        [FromQuery] SupportKind? kind,
        [FromQuery] SupportStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, httpStatus, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(httpStatus, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.SupportTickets.Where(x => x.OperatorId == op!.Id);
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
                (x.Trip != null && x.Trip.Reference.Contains(term)) ||
                (x.Rider != null && x.Rider.AppUser.FullName.Contains(term)) ||
                (x.Customer != null && (x.Customer.FirstName.Contains(term) || x.Customer.LastName.Contains(term))));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Include(x => x.Operator)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.PickupBarangay)
                    .ThenInclude(x => x!.Municipality)
            .Include(x => x.Rider)
                .ThenInclude(x => x!.AppUser)
            .Include(x => x.Customer)
                .ThenInclude(x => x!.AppUser)
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Kind)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var openSos = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var openTickets = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Status == SupportStatus.Open,
            cancellationToken);
        var closedTickets = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Status == SupportStatus.Closed,
            cancellationToken);

        return Ok(new SupportInboxResult(
            rows.Select(OperatorMaps.Support).ToList(),
            page,
            pageSize,
            total,
            openSos,
            openTickets,
            closedTickets,
            0));
    }

    [HttpGet("support/{id:guid}")]
    public async Task<ActionResult<SupportTicketDetailResponse>> SupportTicket(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var ticket = await LoadTicketAsync(op!.Id, id, cancellationToken);
        return ticket is null ? NotFound() : Ok(OperatorMaps.SupportDetail(ticket));
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] RiderPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var userId = AdminAccess.UserId(User);
        var user = userId is null
            ? null
            : await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.Operator, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Operator account not found or inactive." });
        }

        if (!SecretHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (!SecretHasher.IsStrongPassword(request.NewPassword))
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        user.PasswordHash = SecretHasher.Hash(request.NewPassword.Trim());
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password updated." });
    }

    [HttpPost("support/{id:guid}/notes")]
    public async Task<ActionResult<SupportTicketItem>> AddNotes(
        Guid id,
        [FromBody] SupportNoteRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        var note = (request.Notes ?? string.Empty).Trim();
        if (note.Length == 0)
        {
            return BadRequest(new { message = "Add a handling note." });
        }

        var stamp = PhilippineTime.ToPh(DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm");
        ticket.OperatorNotes = string.IsNullOrWhiteSpace(ticket.OperatorNotes)
            ? $"{stamp} — {note}"
            : $"{ticket.OperatorNotes}\n{stamp} — {note}";
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadTicketAsync(op.Id, id, cancellationToken);
        return Ok(OperatorMaps.Support(loaded!));
    }

    [HttpPost("support/{id:guid}/close")]
    public async Task<ActionResult<SupportTicketItem>> Close(
        Guid id,
        [FromBody] CloseTicketRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        ticket.Status = request.Closed ? SupportStatus.Closed : SupportStatus.Open;
        ticket.ClosedAtUtc = request.Closed ? DateTime.UtcNow : null;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadTicketAsync(op.Id, id, cancellationToken);
        return Ok(OperatorMaps.Support(loaded!));
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<OperatorInboxItem>>> Inbox(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.OperatorNotifications
            .Where(x => x.OperatorId == op!.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new OperatorInboxItem(
            x.Id,
            x.Kind,
            x.Title,
            x.Body,
            x.BillId,
            DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
            x.ReadAtUtc is DateTime read ? DateTime.SpecifyKind(read, DateTimeKind.Utc) : null)).ToList());
    }

    [HttpPost("inbox/{id:guid}/read")]
    public async Task<ActionResult<OperatorInboxItem>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorNotifications.FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        row.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new OperatorInboxItem(
            row.Id,
            row.Kind,
            row.Title,
            row.Body,
            row.BillId,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(row.ReadAtUtc.Value, DateTimeKind.Utc)));
    }

    [HttpPost("inbox/read-billing")]
    public async Task<IActionResult> MarkBillingRead(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var now = DateTime.UtcNow;
        await db.OperatorNotifications
            .Where(x => x.OperatorId == op.Id && x.Kind == NotificationKind.Billing && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now), cancellationToken);
        return Ok(new { message = "Billing notifications marked read." });
    }

    [HttpGet("billing")]
    public async Task<ActionResult<BillingOperatorDetail>> Billing(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == op!.Id && x.Status == TripStatus.Completed && x.BillId == null)
            .Select(x => new { x.VehicleType, x.Fare, x.CompletedAtUtc })
            .ToListAsync(cancellationToken);
        var motorcycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Motorcycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));
        var tricycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Tricycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));

        var billRows = await db.OperatorBills
            .Where(x => x.OperatorId == op.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var billIds = billRows.Select(x => x.Id).ToList();
        var billedTrips = billIds.Count == 0
            ? []
            : await db.Trips
                .Where(x => x.BillId != null && billIds.Contains(x.BillId.Value))
                .Select(x => new
                {
                    x.BillId,
                    AtUtc = x.CompletedAtUtc ?? x.RequestedAtUtc,
                    RiderName = x.Rider.AppUser.FullName,
                    x.Reference,
                    x.Fare,
                    x.VehicleType
                })
                .ToListAsync(cancellationToken);

        var bills = billRows.Select(x =>
        {
            var lines = billedTrips
                .Where(t => t.BillId == x.Id)
                .OrderBy(t => t.AtUtc)
                .Select(t => new BillTripItem(
                    DateTime.SpecifyKind(t.AtUtc, DateTimeKind.Utc),
                    t.RiderName,
                    t.Reference,
                    t.Fare,
                    CommissionCut.Round(CommissionCut.Of(
                        t.Fare,
                        t.VehicleType,
                        op.MotorcycleCommissionPercent,
                        op.TricycleCommissionPercent))))
                .ToList();
            return new BillListItem(
                x.Id,
                x.Number,
                x.Status,
                x.Amount,
                x.MotorcycleAmount,
                x.TricycleAmount,
                x.TripCount,
                x.PeriodFromUtc,
                x.PeriodToUtc,
                x.DisabledOperator,
                x.NotifiedAtUtc,
                x.CreatedAtUtc,
                x.Note,
                lines);
        }).ToList();

        var riderCount = await db.RiderProfiles.CountAsync(x => x.OperatorId == op.Id, cancellationToken);
        return Ok(new BillingOperatorDetail(
            op.Id,
            op.CompanyName,
            op.ContactName,
            op.ContactPhone,
            UploadUrls.FromPath(op.ProfilePhotoPath),
            op.IsActive,
            riderCount,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            motorcycle + tricycle,
            motorcycle,
            tricycle,
            trips.Count,
            trips.Count == 0 ? null : trips.Min(x => x.CompletedAtUtc),
            trips.Count == 0 ? null : trips.Max(x => x.CompletedAtUtc),
            bills));
    }

    private async Task<YaPasakay.Domain.Entities.SupportTicket?> LoadTicketAsync(
        Guid operatorId,
        Guid id,
        CancellationToken cancellationToken) =>
        await OperatorMaps.SupportDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.Id == id, cancellationToken);
}
