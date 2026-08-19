using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/schedule")]
public class OperatorScheduleController(AppDbContext db, TripBroadcastService broadcast) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ScheduledBookingItem>>> List(
        [FromQuery] string? q,
        [FromQuery(Name = "status")] TripStatus? tripStatus,
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
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Where(x => x.OperatorId == op!.Id && x.ScheduledAtUtc != null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.CustomerPhone.Contains(phone.Length > 0 ? phone : term) ||
                x.Rider.AppUser.FullName.Contains(term) ||
                x.Rider.PlateNumber.Contains(term));
        }

        if (tripStatus is TripStatus filterStatus)
        {
            query = query.Where(x => x.Status == filterStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Status == TripStatus.Cancelled || x.Status == TripStatus.Completed)
            .ThenBy(x => x.ScheduledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<ScheduledBookingItem>(rows.Select(Map).ToList(), page, pageSize, total));
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
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id && x.ScheduledAtUtc != null, cancellationToken);
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
    }

    [HttpPost]
    public async Task<ActionResult<RideDetailResponse>> Create(
        [FromBody] CreateScheduledBookingRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var name = (request.CustomerName ?? string.Empty).Trim();
        var phone = PhoneNormalizer.Normalize(request.Phone);
        if (name.Length == 0 || phone.Length < 10)
        {
            return BadRequest(new { message = "Customer name and a valid phone number are required." });
        }

        var scheduled = DateTime.SpecifyKind(request.ScheduledAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        if (request.ScheduledAtUtc == default || scheduled < DateTime.UtcNow.AddMinutes(10))
        {
            return BadRequest(new { message = "Schedule the booking at least 10 minutes from now (Philippine time)." });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == request.RiderId && x.OperatorId == op!.Id, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AppUser.IsActive)
        {
            return BadRequest(new { message = "Choose an active rider from your fleet." });
        }

        var pickup = await LoadBarangayAsync(request.PickupBarangayId, cancellationToken);
        var dropoff = await LoadBarangayAsync(request.DropoffBarangayId, cancellationToken);
        if (pickup is null || dropoff is null)
        {
            return BadRequest(new { message = "Choose pickup and drop-off barangays." });
        }

        var pickupDetails = (request.PickupDetails ?? string.Empty).Trim();
        var dropoffDetails = (request.DropoffDetails ?? string.Empty).Trim();
        if (pickupDetails.Length == 0 || dropoffDetails.Length == 0)
        {
            return BadRequest(new { message = "Add pickup and drop-off address details." });
        }

        var distance = request.DistanceKm <= 0 ? 4m : Math.Round(request.DistanceKm, 1, MidpointRounding.AwayFromZero);
        var fare = await QuoteAsync(op.Id, rider.VehicleType, distance, cancellationToken);
        var paymentError = RiderPaymentSync.ValidateTripPayment(request.PaymentMethod, request.PaymentMethodOther);
        if (paymentError is not null)
        {
            return BadRequest(new { message = paymentError });
        }

        if (!await RiderPaymentSync.AcceptsAsync(db, rider.Id, request.PaymentMethod, cancellationToken))
        {
            return BadRequest(new { message = "The assigned rider does not accept that payment method." });
        }

        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.AppUser.PhoneNumber == phone, cancellationToken);

        var trip = new Trip
        {
            OperatorId = op.Id,
            RiderId = rider.Id,
            VehicleType = rider.VehicleType,
            Status = TripStatus.Pending,
            Pickup = OperatorAddressSync.Format(pickupDetails, pickup),
            PickupDetails = pickupDetails,
            PickupBarangayId = pickup.Id,
            Dropoff = OperatorAddressSync.Format(dropoffDetails, dropoff),
            DropoffDetails = dropoffDetails,
            DropoffBarangayId = dropoff.Id,
            CustomerId = customer?.Id,
            CustomerName = name,
            CustomerPhone = phone,
            Reference = $"YP{scheduled:yyyyMMdd}-S{Random.Shared.Next(10, 99):00}{DateTime.UtcNow:ss}",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Fare = fare,
            DistanceKm = distance,
            PaymentMethod = request.PaymentMethod,
            PaymentMethodOther = request.PaymentMethod == PaymentMethod.Other
                ? request.PaymentMethodOther?.Trim()
                : null,
            RequestedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = scheduled
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.BroadcastAsync(trip.Id, cancellationToken);

        var loaded = await OperatorMaps.RideDetailQuery(db)
            .FirstAsync(x => x.Id == trip.Id, cancellationToken);
        return Ok(OperatorMaps.RideDetail(loaded));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RideDetailResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id && x.ScheduledAtUtc != null, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Ongoing)
        {
            return BadRequest(new { message = "This scheduled booking can no longer be cancelled." });
        }

        trip.Status = TripStatus.Cancelled;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelReason = "Customer cancelled the scheduled booking.";
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        return Ok(OperatorMaps.RideDetail(trip));
    }

    private async Task<Barangay?> LoadBarangayAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private async Task<decimal> QuoteAsync(Guid operatorId, VehicleType vehicleType, decimal distanceKm, CancellationToken cancellationToken)
    {
        var fare = await db.FareMatrices
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.VehicleType == vehicleType && x.IsActive, cancellationToken);
        if (fare is null)
        {
            return FareQuote.Compute(50, 12, 50, 1, distanceKm);
        }

        return FareQuote.Compute(fare.BaseFare, fare.PerKm, fare.MinimumFare, fare.IncludedKm, distanceKm);
    }

    private static ScheduledBookingItem Map(Trip trip) =>
        new(
            trip.Id,
            trip.Reference,
            DateTime.SpecifyKind(trip.ScheduledAtUtc!.Value, DateTimeKind.Utc),
            trip.CustomerName,
            trip.CustomerPhone,
            trip.RiderId,
            trip.Rider.AppUser.FullName,
            trip.Rider.PlateNumber,
            trip.VehicleType,
            trip.Pickup,
            trip.Dropoff,
            trip.Status,
            trip.Fare,
            trip.PaymentMethod,
            trip.PaymentMethodOther);
}
