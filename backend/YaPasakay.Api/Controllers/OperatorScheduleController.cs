using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/schedule")]
public class OperatorScheduleController(
    AppDbContext db,
    TripBroadcastService broadcast,
    GoogleDrivingDistance driving) : ControllerBase
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
            .ThenInclude(x => x!.AppUser)
            .Where(x => x.OperatorId == op!.Id && x.ScheduledAtUtc != null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.CustomerPhone.Contains(phone.Length > 0 ? phone : term) ||
                (x.Rider != null && x.Rider.AppUser.FullName.Contains(term)) ||
                (x.Rider != null && x.Rider.PlateNumber.Contains(term)));
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
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        return trip is null ? NotFound() : Ok(await OperatorMaps.RideDetailAsync(trip, db, cancellationToken));
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

        if (request.PaymentMethod is not (PaymentMethod.Cash or PaymentMethod.GCash or PaymentMethod.Maya))
        {
            return BadRequest(new { message = "Choose Cash, GCash, or Maya." });
        }

        var paymentError = RiderPaymentSync.ValidateTripPayment(request.PaymentMethod, request.PaymentMethodOther);
        if (paymentError is not null)
        {
            return BadRequest(new { message = paymentError });
        }

        DateTime? scheduled = null;
        if (!request.IsImmediate)
        {
            if (request.ScheduledAtUtc is null || request.ScheduledAtUtc == default)
            {
                return BadRequest(new { message = "Set the pickup date and time in Philippine time." });
            }

            scheduled = DateTime.SpecifyKind(request.ScheduledAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (scheduled < DateTime.UtcNow.AddMinutes(10))
            {
                return BadRequest(new { message = "Schedule the booking at least 10 minutes from now (Philippine time)." });
            }
        }

        var pickupLat = request.PickupLat;
        var pickupLng = request.PickupLng;
        var dropoffLat = request.DropoffLat;
        var dropoffLng = request.DropoffLng;
        if (pickupLat == 0 || pickupLng == 0 || dropoffLat == 0 || dropoffLng == 0)
        {
            return BadRequest(new { message = "Set pickup and drop-off on the map." });
        }

        var pickupDetails = (request.PickupDetails ?? string.Empty).Trim();
        var dropoffDetails = (request.DropoffDetails ?? string.Empty).Trim();
        if (pickupDetails.Length == 0 || dropoffDetails.Length == 0)
        {
            return BadRequest(new { message = "Add pickup and drop-off address details." });
        }

        var pickup = await TerritoryLookup.MatchFromAddressAsync(
            db, request.PickupBarangayId, pickupDetails, cancellationToken);
        var dropoff = await TerritoryLookup.MatchFromAddressAsync(
            db, request.DropoffBarangayId, dropoffDetails, cancellationToken);
        if (pickup is null || dropoff is null)
        {
            return BadRequest(new { message = "Pickup and drop-off must match a Philippine barangay." });
        }

        var coverage = await OperatorAreaSync.CoverageErrorAsync(db, op.Id, pickup.Id, cancellationToken);
        if (coverage is not null)
        {
            return BadRequest(new { message = coverage });
        }

        var (vehicle, vehicleCategoryId, maxPassengers, isCargo, vehicleError) =
            await ResolveVehicleAsync(op.Id, request.VehicleType, request.VehicleCategoryId, cancellationToken);
        if (vehicleError is not null)
        {
            return BadRequest(new { message = vehicleError });
        }

        var selectMode = request.RiderId is Guid;
        RiderProfile? assignedRider = null;
        if (selectMode)
        {
            assignedRider = await db.RiderProfiles
                .Include(x => x.AppUser)
                .Include(x => x.Wallet)
                .FirstOrDefaultAsync(x => x.Id == request.RiderId && x.OperatorId == op.Id, cancellationToken);
            if (assignedRider is null || !assignedRider.IsActive || !assignedRider.AppUser.IsActive)
            {
                return BadRequest(new { message = "Choose an active rider from your fleet." });
            }

            if (!RiderMatchesVehicle(assignedRider, vehicle, vehicleCategoryId))
            {
                return BadRequest(new { message = "That rider does not match the selected vehicle type." });
            }

            var balance = assignedRider.Wallet?.Balance ?? 0;
            if (!TripBroadcastService.CanReceiveBookings(balance))
            {
                return BadRequest(new { message = TripBroadcastService.WalletBlockedMessage(balance) });
            }
        }

        var (measuredKm, _) = await driving.MeasureAsync(pickupLat, pickupLng, dropoffLat, dropoffLng, cancellationToken);
        var distance = request.DistanceKm > 0
            ? Math.Round(request.DistanceKm, 1, MidpointRounding.AwayFromZero)
            : (measuredKm > 0 ? Math.Round(measuredKm, 1, MidpointRounding.AwayFromZero) : 4m);

        var fareRow = await OperatorMaps.LoadFareMatrixAsync(
            db, op.Id, vehicle, pickup.MunicipalityId, vehicleCategoryId, cancellationToken);
        if (fareRow is null)
        {
            return BadRequest(new { message = "No fare matrix for this vehicle in the pickup municipality. Create rates first." });
        }

        var passengers = VehicleCatalog.ClampPassengers(vehicle, isCargo, maxPassengers, request.PassengerCount);
        var fare = DeriveFarePricingService.ComputeMunicipalityWithSurcharges(fareRow, passengers, distance);

        RiderProfile? tripRider = assignedRider;
        if (scheduled is null)
        {
            tripRider = assignedRider ?? await PickProvisionalRiderAsync(
                op.Id, vehicle, vehicleCategoryId, pickupLat, pickupLng, cancellationToken);
            if (tripRider is null)
            {
                return BadRequest(new
                {
                    message = selectMode
                        ? "Choose an active rider from your fleet."
                        : "No rider is available for that vehicle type yet. Add a rider or use Select."
                });
            }
        }

        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.AppUser.PhoneNumber == phone, cancellationToken);

        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            OperatorId = op.Id,
            RiderId = tripRider?.Id,
            VehicleType = vehicle,
            VehicleCategoryId = vehicleCategoryId
                ?? (VehicleTypeRules.IsKnown(vehicle) ? VehicleCatalog.IdFor(vehicle) : null),
            Status = scheduled is null ? TripStatus.Pending : TripStatus.Scheduled,
            Pickup = pickupDetails,
            PickupDetails = pickupDetails,
            PickupBarangayId = pickup.Id,
            PickupLat = pickupLat,
            PickupLng = pickupLng,
            Dropoff = dropoffDetails,
            DropoffDetails = dropoffDetails,
            DropoffBarangayId = dropoff.Id,
            DropoffLat = dropoffLat,
            DropoffLng = dropoffLng,
            CustomerId = customer?.Id,
            CustomerName = name,
            CustomerPhone = phone,
            Reference = scheduled is DateTime at
                ? $"YP{at:yyyyMMdd}-S{Random.Shared.Next(10, 99):00}{now:ss}"
                : $"YP{now:yyyyMMdd}-O{Random.Shared.Next(10, 99):00}{now:ss}",
            Notes = selectMode
                ? TripBroadcastService.CustomerPickNote
                : (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()),
            Fare = fare,
            CustomerFare = fare,
            DistanceKm = distance,
            PassengerCount = passengers,
            PaymentMethod = request.PaymentMethod,
            PaymentMethodOther = null,
            RequestedAtUtc = now,
            ScheduledAtUtc = scheduled
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync(cancellationToken);
        if (scheduled is null)
        {
            await broadcast.BroadcastAsync(trip.Id, cancellationToken);
        }
        else if (tripRider is not null)
        {
            await broadcast.OfferToAssignedRiderAsync(trip.Id, tripRider.Id, cancellationToken);
        }

        var loaded = await OperatorMaps.RideDetailQuery(db)
            .FirstAsync(x => x.Id == trip.Id, cancellationToken);
        return Ok(await OperatorMaps.RideDetailAsync(loaded, db, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RideDetailResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        // Allow cancel for scheduled rows and Immediate desk bookings created from this module.
        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is not (TripStatus.Pending or TripStatus.Waiting or TripStatus.Scheduled or TripStatus.ScheduledAccepted))
        {
            return BadRequest(new { message = "This booking can no longer be cancelled." });
        }

        trip.Status = TripStatus.Cancelled;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelReason = trip.ScheduledAtUtc is null
            ? "Operator cancelled the booking."
            : "Operator cancelled the scheduled booking.";
        trip.CancelledBy = CancelledBy.Operator;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        return Ok(await OperatorMaps.RideDetailAsync(trip, db, cancellationToken));
    }

    private async Task<(VehicleType Vehicle, Guid? CategoryId, int MaxPassengers, bool IsCargo, string? Error)> ResolveVehicleAsync(
        Guid operatorId,
        VehicleType requestedType,
        Guid? requestedCategoryId,
        CancellationToken cancellationToken)
    {
        Guid? categoryId = requestedCategoryId;
        var vehicle = requestedType;
        var maxPassengers = VehicleTypeRules.MaxPassengers(vehicle);
        var isCargo = VehicleTypeRules.IsCargo(vehicle);

        if (categoryId is Guid catId)
        {
            var offer = await db.OperatorVehicleOffers
                .AsNoTracking()
                .Include(x => x.VehicleCategory)
                .FirstOrDefaultAsync(
                    x => x.OperatorId == operatorId && x.VehicleCategoryId == catId && x.IsEnabled,
                    cancellationToken);
            if (offer?.VehicleCategory is null)
            {
                return (vehicle, null, maxPassengers, isCargo, "That vehicle type is not available for your fleet.");
            }

            var cat = offer.VehicleCategory;
            categoryId = cat.Id;
            if (cat.LegacyEnumValue is int legacy && Enum.IsDefined(typeof(VehicleType), legacy))
            {
                vehicle = (VehicleType)legacy;
            }
            else if (VehicleCatalog.TypeFor(cat.Id) is VehicleType platformType)
            {
                vehicle = platformType;
            }
            else
            {
                vehicle = VehicleType.Custom;
            }

            maxPassengers = offer.MaxPassengers ?? cat.MaxPassengers;
            isCargo = cat.IsCargo;
            return (vehicle, categoryId, maxPassengers, isCargo, null);
        }

        if (VehicleTypeRules.ValidateChoice(vehicle) is { } invalid)
        {
            return (vehicle, null, maxPassengers, isCargo, invalid);
        }

        var presetId = VehicleCatalog.IdFor(vehicle);
        var enabled = await db.OperatorVehicleOffers
            .AsNoTracking()
            .AnyAsync(x => x.OperatorId == operatorId && x.VehicleCategoryId == presetId && x.IsEnabled, cancellationToken);
        if (!enabled)
        {
            return (vehicle, null, maxPassengers, isCargo, "That vehicle type is not enabled for your fleet.");
        }

        return (vehicle, presetId, maxPassengers, isCargo, null);
    }

    private static bool RiderMatchesVehicle(RiderProfile rider, VehicleType vehicle, Guid? vehicleCategoryId)
    {
        if (vehicle == VehicleType.Custom)
        {
            return vehicleCategoryId is Guid cat
                && rider.VehicleType == VehicleType.Custom
                && rider.VehicleCategoryId == cat;
        }

        if (rider.VehicleType != vehicle)
        {
            return false;
        }

        if (vehicleCategoryId is Guid preferred && rider.VehicleCategoryId is Guid riderCat)
        {
            return riderCat == preferred;
        }

        return true;
    }

    private async Task<RiderProfile?> PickProvisionalRiderAsync(
        Guid operatorId,
        VehicleType vehicleType,
        Guid? vehicleCategoryId,
        double pickupLat,
        double pickupLng,
        CancellationToken cancellationToken)
    {
        IQueryable<RiderProfile> query = db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Wallet)
            .Where(x => x.OperatorId == operatorId && x.IsActive && x.AcceptsPasakay && x.AppUser.IsActive);

        if (vehicleType == VehicleType.Custom)
        {
            if (vehicleCategoryId is not Guid categoryId)
            {
                return null;
            }

            query = query.Where(x => x.VehicleType == VehicleType.Custom && x.VehicleCategoryId == categoryId);
        }
        else
        {
            query = query.Where(x => x.VehicleType == vehicleType);
        }

        var riders = await query.ToListAsync(cancellationToken);
        if (vehicleCategoryId is Guid preferredCategoryId && vehicleType != VehicleType.Custom)
        {
            var matched = riders.Where(x => x.VehicleCategoryId == preferredCategoryId).ToList();
            if (matched.Count > 0)
            {
                riders = matched;
            }
        }

        if (riders.Count == 0)
        {
            return null;
        }

        var funded = riders.Where(x => TripBroadcastService.CanReceiveBookings(x.Wallet?.Balance ?? 0)).ToList();
        var pool = funded.Count > 0 ? funded : riders;
        return pool
            .OrderByDescending(x => x.IsOnline)
            .ThenBy(x => Geo.DistanceKm(x.LastLat, x.LastLng, pickupLat, pickupLng) ?? double.MaxValue)
            .First();
    }

    private static ScheduledBookingItem Map(Trip trip) =>
        new(
            trip.Id,
            trip.Reference,
            DateTime.SpecifyKind(trip.ScheduledAtUtc!.Value, DateTimeKind.Utc),
            trip.CustomerName,
            trip.CustomerPhone,
            trip.RiderId,
            trip.Rider?.AppUser?.FullName ?? "Unassigned",
            trip.Rider?.PlateNumber ?? "—",
            trip.VehicleType,
            TripAddress.Display(trip.PickupDetails, trip.Pickup),
            TripAddress.Display(trip.DropoffDetails, trip.Dropoff),
            trip.Status,
            trip.Fare,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            trip.CustomerFare > 0 ? trip.CustomerFare : trip.Fare,
            FareDiscountRules.LabelOrNull(trip.FareDiscountKind, trip.FareDiscountNote, trip.FareDiscountPercent, trip.FareDiscountAmount));
}
