using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/bookings")]
public class OperatorBookingsController(AppDbContext db, RiderWalletService wallets, TripBroadcastService broadcast) : ControllerBase
{
    private const int ColumnSize = 40;

    [HttpGet]
    public async Task<ActionResult<OperatorBookingBoardResponse>> Board(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var (start, endExclusive) = ResolveBoardWindow(from, to);
        var includesNow = DateTime.UtcNow >= start && DateTime.UtcNow < endExclusive;
        var query = db.Trips.Where(x => x.OperatorId == op!.Id);
        return Ok(new OperatorBookingBoardResponse(
            await ColumnAsync(query, TripStatus.Pending, start, endExclusive, includesNow, cancellationToken),
            await ColumnAsync(query, TripStatus.Waiting, start, endExclusive, includesNow, cancellationToken),
            await ColumnAsync(query, TripStatus.Ongoing, start, endExclusive, includesNow, cancellationToken),
            await ColumnAsync(query, TripStatus.Completed, start, endExclusive, includesNow, cancellationToken)));
    }

    [HttpGet("list")]
    public async Task<ActionResult<PagedResult<OperatorBookingListItem>>> List(
        [FromQuery] string? q,
        [FromQuery(Name = "status")] TripStatus? tripStatus,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Trips
            .AsNoTracking()
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Where(x => x.OperatorId == op!.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            var phoneTerm = phone.Length > 0 ? phone : term;
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.CustomerPhone.Contains(phoneTerm) ||
                x.Pickup.Contains(term) ||
                x.Dropoff.Contains(term) ||
                x.Rider.AppUser.FullName.Contains(term) ||
                x.Rider.PlateNumber.Contains(term));
        }

        if (tripStatus is TripStatus filterStatus)
        {
            query = query.Where(x => x.Status == filterStatus);
        }

        if (from is not null || to is not null)
        {
            var (start, endExclusive) = ResolveBoardWindow(from, to);
            query = query.Where(x =>
                (x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                && (x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Status == TripStatus.Completed || x.Status == TripStatus.Cancelled ? 2 : 0)
            .ThenByDescending(x => x.ScheduledAtUtc ?? x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OperatorBookingListItem(
                x.Id,
                x.Reference,
                x.RequestedAtUtc,
                x.ScheduledAtUtc,
                x.CustomerName,
                x.CustomerPhone,
                x.Rider.AppUser.FullName,
                x.Rider.PlateNumber,
                x.VehicleType,
                x.Pickup,
                x.Dropoff,
                x.Status,
                x.Fare,
                x.PaymentMethod,
                x.PaymentMethodOther))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<OperatorBookingListItem>(
            rows.Select(x => x with
            {
                RequestedAtUtc = DateTime.SpecifyKind(x.RequestedAtUtc, DateTimeKind.Utc),
                ScheduledAtUtc = x.ScheduledAtUtc is DateTime scheduled
                    ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc)
                    : null
            }).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RideDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
    }

    [HttpPost("{id:guid}/reassign")]
    public async Task<ActionResult<RideDetailResponse>> Reassign(
        Guid id,
        [FromBody] ReassignBookingRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Ongoing)
        {
            return BadRequest(new { message = "This booking can no longer be reassigned." });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == request.RiderId && x.OperatorId == op!.Id, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AppUser.IsActive)
        {
            return BadRequest(new { message = "Choose an active rider from your fleet." });
        }

        if (rider.Id == trip.RiderId)
        {
            return BadRequest(new { message = "This rider already has the booking." });
        }

        if (!await RiderPaymentSync.AcceptsAsync(db, rider.Id, trip.PaymentMethod, cancellationToken))
        {
            return BadRequest(new { message = "The new rider does not accept this booking's payment method." });
        }

        var fromName = trip.Rider.AppUser.FullName;
        trip.RiderId = rider.Id;
        trip.VehicleType = rider.VehicleType;
        trip.Fare = await QuoteAsync(op.Id, rider.VehicleType, trip.DistanceKm, cancellationToken);
        trip.UpdatedAtUtc = DateTime.UtcNow;
        var note = $"Reassigned from {fromName} to {rider.AppUser.FullName}.";
        trip.Notes = string.IsNullOrWhiteSpace(trip.Notes)
            ? note
            : trip.Notes.Length + 1 + note.Length <= 200
                ? $"{trip.Notes} {note}"
                : trip.Notes;
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.BroadcastAsync(trip.Id, cancellationToken);

        var loaded = await OperatorMaps.RideDetailQuery(db)
            .FirstAsync(x => x.Id == trip.Id, cancellationToken);
        return Ok(OperatorMaps.RideDetail(loaded));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<RideDetailResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            return BadRequest(new { message = "This booking is already finished." });
        }

        trip.Status = TripStatus.Completed;
        trip.CompletedAtUtc = DateTime.UtcNow;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        await wallets.ApplyCommissionAsync(trip, cancellationToken);

        var loaded = await OperatorMaps.RideDetailQuery(db)
            .FirstAsync(x => x.Id == trip.Id, cancellationToken);
        return Ok(OperatorMaps.RideDetail(loaded));
    }

    private async Task<decimal> QuoteAsync(Guid operatorId, VehicleType vehicleType, decimal distanceKm, CancellationToken cancellationToken)
    {
        var fare = await db.FareMatrices
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.VehicleType == vehicleType && x.IsActive, cancellationToken);
        if (fare is null)
        {
            return FareQuote.Compute(50, 12, 50, 1, distanceKm <= 0 ? 4 : distanceKm);
        }

        return FareQuote.Compute(fare.BaseFare, fare.PerKm, fare.MinimumFare, fare.IncludedKm, distanceKm <= 0 ? 4 : distanceKm);
    }

    private static (DateTime Start, DateTime EndExclusive) ResolveBoardWindow(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(PhilippineTime.ToPh(DateTime.UtcNow));
        var startDate = from ?? to ?? today;
        var endDate = to ?? from ?? today;
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var start = PhilippineTime.ToUtc(startDate.Year, startDate.Month, startDate.Day);
        var next = endDate.AddDays(1);
        var endExclusive = PhilippineTime.ToUtc(next.Year, next.Month, next.Day);
        return (start, endExclusive);
    }

    private static async Task<OperatorBookingColumn> ColumnAsync(
        IQueryable<YaPasakay.Domain.Entities.Trip> query,
        TripStatus status,
        DateTime start,
        DateTime endExclusive,
        bool includesNow,
        CancellationToken cancellationToken)
    {
        var filtered = query.Where(x => x.Status == status && (status != TripStatus.Pending || x.ScheduledAtUtc == null));
        var live = status is TripStatus.Pending or TripStatus.Waiting or TripStatus.Ongoing;
        if (!(live && includesNow))
        {
            if (status == TripStatus.Completed)
            {
                filtered = filtered.Where(x =>
                    (x.CompletedAtUtc ?? x.RequestedAtUtc) >= start
                    && (x.CompletedAtUtc ?? x.RequestedAtUtc) < endExclusive);
            }
            else
            {
                filtered = filtered.Where(x => x.RequestedAtUtc >= start && x.RequestedAtUtc < endExclusive);
            }
        }

        var total = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(ColumnSize)
            .Select(x => new RideListItem(
                x.Id,
                x.Reference,
                x.RequestedAtUtc,
                x.Pickup,
                x.Dropoff,
                x.CustomerName,
                x.VehicleType,
                x.Status,
                x.Fare,
                x.DistanceKm,
                x.PaymentMethod,
                x.PaymentMethodOther))
            .ToListAsync(cancellationToken);
        return new OperatorBookingColumn(total, items);
    }
}
