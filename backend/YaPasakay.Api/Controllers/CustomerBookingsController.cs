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
[Authorize(Roles = "Customer")]
[Route("api/customer")]
public class CustomerBookingsController(
    AppDbContext db,
    TripBroadcastService broadcast,
    TripChatRealtime chatRealtime,
    LiveNotify live,
    GoogleDrivingDistance driving,
    UploadStore uploads) : ControllerBase
{

    [HttpGet("desk")]
    public async Task<ActionResult<CustomerDeskResponse>> Desk(CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    [HttpGet("places")]
    public async Task<ActionResult<IReadOnlyList<CustomerPlaceItem>>> Places(CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await CustomerDeskBuilder.LoadPlacesAsync(db, cancellationToken));
    }

    [HttpGet("riders/{id:guid}")]
    public async Task<ActionResult<CustomerHailRider>> HailRider(Guid id, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AppUser.IsActive || !rider.Operator.IsActive)
        {
            return NotFound(new { message = "This QR is not a Ya! Pasakay rider." });
        }

        var busy = await db.Trips.AnyAsync(
            x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);

        return Ok(new CustomerHailRider(
            rider.Id,
            rider.AppUser.FullName,
            rider.PlateNumber,
            rider.VehicleType,
            rider.VehicleModel,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            rider.AppUser.PhoneNumber,
            rider.IsOnline,
            busy,
            rider.Operator.CompanyName,
            rider.PaymentMethods.Select(x => x.Method).OrderBy(x => x).ToList()));
    }

    [HttpGet("trips/{id:guid}/chat")]
    public async Task<ActionResult<IReadOnlyList<RideChatMessageItem>>> Chat(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (!TripChatService.CanView(trip))
        {
            return BadRequest(new { message = "Chat is not available for this trip." });
        }

        return Ok(await TripChatService.ListAsync(db, trip.Id, cancellationToken));
    }

    [HttpGet("trips/{id:guid}")]
    public async Task<ActionResult<RideDetailResponse>> TripDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
    }

    [HttpPost("trips/{id:guid}/chat")]
    public async Task<ActionResult<RideChatMessageItem>> SendChat(
        Guid id,
        [FromBody] TripChatSendRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var sent = await TripChatService.SendAsync(db, trip, ChatSender.Customer, request.Body, null, cancellationToken);
        if (sent.Message is null)
        {
            return BadRequest(new { message = sent.Error });
        }

        await chatRealtime.BroadcastAsync(trip, sent.Message, cancellationToken);
        await live.ChatMessageAsync(trip, sent.Message, cancellationToken);
        return Ok(sent.Message);
    }

    [HttpPost("trips/{id:guid}/chat/photo")]
    [RequestSizeLimit(TripChatService.MaxPhotoBytes + 1_000_000)]
    public async Task<ActionResult<RideChatMessageItem>> SendChatPhoto(
        Guid id,
        [FromForm] string? body,
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var saved = await TripChatService.SavePhotoAsync(uploads, photo, trip.Id, cancellationToken);
        if (saved.Path is null)
        {
            return BadRequest(new { message = saved.Error });
        }

        var sent = await TripChatService.SendAsync(db, trip, ChatSender.Customer, body, saved.Path, cancellationToken);
        if (sent.Message is null)
        {
            return BadRequest(new { message = sent.Error });
        }

        await chatRealtime.BroadcastAsync(trip, sent.Message, cancellationToken);
        await live.ChatMessageAsync(trip, sent.Message, cancellationToken);
        return Ok(sent.Message);
    }

    [HttpPost("quote")]
    public async Task<ActionResult<CustomerQuoteResponse>> Quote(
        [FromBody] CustomerBookRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var body = BindHail(customer, request);
        var prepared = await PrepareAsync(body, requireHailReady: false, requireRider: false, cancellationToken);
        if (prepared.Error is not null)
        {
            return BadRequest(new { message = prepared.Error });
        }

        return Ok(new CustomerQuoteResponse(
            prepared.Fare,
            prepared.DistanceKm,
            prepared.EtaMinutes,
            prepared.Operator!.CompanyName,
            prepared.VehicleType,
            body.PaymentMethod,
            prepared.Rider is not null));
    }

    [HttpPost("service-check")]
    public async Task<ActionResult<CustomerServiceCheckResponse>> ServiceCheck(
        [FromBody] CustomerServiceCheckRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var pickupDetails = (request.PickupDetails ?? string.Empty).Trim();
        var dropoffDetails = (request.DropoffDetails ?? string.Empty).Trim();
        if (pickupDetails.Length == 0 || dropoffDetails.Length == 0)
        {
            return Ok(new CustomerServiceCheckResponse(true, null));
        }

        var pickup = await ResolveBarangayAsync(request.PickupBarangayId, pickupDetails, cancellationToken);
        Guid? municipalityId = pickup?.MunicipalityId;
        string? municipalityName = pickup?.Municipality.Name;
        if (municipalityId is null)
        {
            var municipality = await TerritoryLookup.MatchMunicipalityFromAddressAsync(db, pickupDetails, cancellationToken);
            municipalityId = municipality?.Id;
            municipalityName = municipality?.Name;
        }

        if (municipalityId is null)
        {
            return Ok(new CustomerServiceCheckResponse(false, municipalityName));
        }

        var municipalityHasOperator = await db.OperatorBarangays.AnyAsync(
            x => x.Operator.IsActive && x.Barangay.MunicipalityId == municipalityId,
            cancellationToken);

        return Ok(new CustomerServiceCheckResponse(municipalityHasOperator, municipalityName));
    }

    [HttpPost("book")]
    public async Task<ActionResult<CustomerDeskResponse>> Book(
        [FromBody] CustomerBookRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var busyNow = request.ScheduledAtUtc is null && await db.Trips.AnyAsync(
            x => x.CustomerId == customer.Id
                && (x.Status == TripStatus.Pending || x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing)
                && (x.ScheduledAtUtc == null || x.ScheduledAtUtc <= DateTime.UtcNow),
            cancellationToken);
        if (busyNow)
        {
            return BadRequest(new { message = "Finish or cancel your current booking first." });
        }

        var body = request.ScheduledAtUtc is null
            ? BindHail(customer, request)
            : request with { RiderId = null, HailQr = false };
        DateTime? scheduled = null;
        if (body.ScheduledAtUtc is DateTime requested)
        {
            scheduled = DateTime.SpecifyKind(requested.ToUniversalTime(), DateTimeKind.Utc);
            if (scheduled < DateTime.UtcNow.AddMinutes(10))
            {
                return BadRequest(new { message = "Schedule the booking at least 10 minutes from now." });
            }
        }

        if (body.HailQr && body.RiderId is null)
        {
            return BadRequest(new { message = "Ask the rider to scan your QR first, then confirm the trip." });
        }

        var prepared = await PrepareAsync(body, requireHailReady: body.RiderId is Guid, requireRider: true, cancellationToken);
        if (prepared.Error is not null || prepared.Operator is null || prepared.Rider is null || prepared.Pickup is null || prepared.Dropoff is null)
        {
            return BadRequest(new { message = prepared.Error ?? "Could not create this booking." });
        }

        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            OperatorId = prepared.Operator.Id,
            RiderId = prepared.Rider.Id,
            VehicleType = prepared.VehicleType,
            Status = body.RiderId is Guid ? TripStatus.Waiting : TripStatus.Pending,
            Pickup = OperatorAddressSync.Format(prepared.PickupDetails, prepared.Pickup),
            PickupDetails = prepared.PickupDetails,
            PickupBarangayId = prepared.Pickup.Id,
            PickupLat = prepared.PickupLat,
            PickupLng = prepared.PickupLng,
            Dropoff = OperatorAddressSync.Format(prepared.DropoffDetails, prepared.Dropoff),
            DropoffDetails = prepared.DropoffDetails,
            DropoffBarangayId = prepared.Dropoff.Id,
            DropoffLat = prepared.DropoffLat,
            DropoffLng = prepared.DropoffLng,
            CustomerId = customer.Id,
            CustomerName = customer.DisplayName,
            CustomerPhone = customer.AppUser.PhoneNumber,
            Reference = scheduled is DateTime at
                ? $"YP{at:yyyyMMdd}-S{Random.Shared.Next(10, 99):00}{now:ss}"
                : $"YP{now:yyyyMMdd}-C{Random.Shared.Next(10, 99):00}{now:ss}",
            Notes = body.HailQr || body.RiderId is Guid
                ? TripBroadcastService.DirectHailNote
                : string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            Fare = prepared.Fare,
            DistanceKm = prepared.DistanceKm,
            PaymentMethod = body.PaymentMethod,
            PaymentMethodOther = body.PaymentMethod == PaymentMethod.Cash || string.IsNullOrWhiteSpace(body.PaymentMethodOther)
                ? null
                : body.PaymentMethodOther!.Trim(),
            RequestedAtUtc = now,
            ScheduledAtUtc = scheduled
        };
        db.Trips.Add(trip);
        if (body.RiderId is Guid)
        {
            customer.HailRiderId = null;
            customer.HailAtUtc = null;
            var distance = Geo.DistanceKm(prepared.Rider.LastLat, prepared.Rider.LastLng, prepared.PickupLat, prepared.PickupLng);
            db.TripOffers.Add(new TripOffer
            {
                TripId = trip.Id,
                RiderId = prepared.Rider.Id,
                Status = OfferStatus.Accepted,
                IsPreferred = true,
                DistanceKm = distance is double km ? Math.Round((decimal)km, 2) : null,
                OfferedAtUtc = now,
                ExpiresAtUtc = now.Add(TripBroadcastService.LiveOfferTtl),
                RespondedAtUtc = now
            });
            await db.SaveChangesAsync(cancellationToken);
            await live.RiderChangedAsync(prepared.Rider.Id, "hail-booked", cancellationToken);
            await live.CustomerChangedAsync(customer.Id, "hail-booked", cancellationToken);
            return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
        }

        await db.SaveChangesAsync(cancellationToken);
        await broadcast.BroadcastAsync(trip.Id, cancellationToken);
        await live.CustomerChangedAsync(customer.Id, "booked", cancellationToken);

        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    [HttpPost("trips/{id:guid}/rate")]
    public async Task<ActionResult<CustomerDeskResponse>> Rate(
        Guid id,
        [FromBody] CustomerRateRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { message = "Pick a rating from 1 to 5 stars." });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status != TripStatus.Completed)
        {
            return BadRequest(new { message = "You can rate after the trip is completed." });
        }

        if (trip.Rating is not null)
        {
            return BadRequest(new { message = "You already rated this trip." });
        }

        var comment = (request.Comment ?? string.Empty).Trim();
        if (comment.Length > 200)
        {
            comment = comment[..200];
        }

        trip.Rating = request.Rating;
        trip.RatingComment = string.IsNullOrEmpty(comment) ? null : comment;
        trip.RatedAtUtc = DateTime.UtcNow;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    [HttpPost("trips/{id:guid}/cancel")]
    public async Task<ActionResult<CustomerDeskResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Ongoing)
        {
            return BadRequest(new { message = "This trip can no longer be cancelled." });
        }

        trip.Status = TripStatus.Cancelled;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelReason = "Customer cancelled the booking.";
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        await live.TripPartiesAsync(trip, "cancelled", cancellationToken);
        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    [HttpPost("hail/clear")]
    public async Task<ActionResult<CustomerDeskResponse>> ClearHail(CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        customer.HailRiderId = null;
        customer.HailAtUtc = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    private async Task<PreparedBooking> PrepareAsync(CustomerBookRequest request, bool requireHailReady, bool requireRider, CancellationToken cancellationToken)
    {
        var pickupDetails = (request.PickupDetails ?? string.Empty).Trim();
        var dropoffDetails = (request.DropoffDetails ?? string.Empty).Trim();
        if (pickupDetails.Length == 0 || dropoffDetails.Length == 0)
        {
            return new PreparedBooking { Error = "Add pickup and drop-off details." };
        }

        var paymentError = RiderPaymentSync.ValidateTripPayment(request.PaymentMethod, request.PaymentMethodOther);
        if (paymentError is not null)
        {
            return new PreparedBooking { Error = paymentError };
        }

        var pickupLat = request.PickupLat ?? 0;
        var pickupLng = request.PickupLng ?? 0;
        var dropoffLat = request.DropoffLat ?? 0;
        var dropoffLng = request.DropoffLng ?? 0;
        if (pickupLat == 0 || dropoffLat == 0)
        {
            return new PreparedBooking { Error = "Set pickup and drop-off on the map." };
        }

        var pickup = await ResolveBarangayAsync(request.PickupBarangayId, pickupDetails, cancellationToken);
        var dropoff = await ResolveBarangayAsync(request.DropoffBarangayId, dropoffDetails, cancellationToken);
        if (pickup is null || dropoff is null)
        {
            return new PreparedBooking { Error = "Pickup and drop-off must match a Philippine barangay." };
        }

        var hail = await ResolveHailRiderAsync(request.RiderId, cancellationToken);
        if (request.RiderId is Guid && hail.Error is not null)
        {
            return new PreparedBooking { Error = hail.Error };
        }

        var vehicle = hail.Rider?.VehicleType ?? request.VehicleType;
        var op = hail.Rider?.Operator;
        op ??= await db.Operators
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.BarangayId == pickup.Id)
                || x.Areas.Any(a => a.Barangay.MunicipalityId == pickup.MunicipalityId)))
            .OrderBy(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
        if (op is null)
        {
            return new PreparedBooking { Error = "No operator covers this pickup area yet." };
        }

        var coverage = await OperatorAreaSync.CoverageErrorAsync(db, op.Id, pickup.Id, cancellationToken);
        if (coverage is not null)
        {
            return new PreparedBooking
            {
                Error = hail.Rider is null
                    ? coverage
                    : "This pickup is outside that rider's operator service area."
            };
        }

        var (distance, eta) = await driving.MeasureAsync(pickupLat, pickupLng, dropoffLat, dropoffLng, cancellationToken);
        var fareRow = await db.FareMatrices
            .FirstOrDefaultAsync(x => x.OperatorId == op.Id && x.VehicleType == vehicle && x.IsActive, cancellationToken);
        var fare = fareRow is null
            ? FareQuote.Compute(50, 12, 50, 1, distance)
            : FareQuote.Compute(fareRow.BaseFare, fareRow.PerKm, fareRow.MinimumFare, fareRow.IncludedKm, distance);

        var rider = hail.Rider ?? await PickRiderAsync(
            op.Id,
            vehicle,
            request.PaymentMethod,
            pickupLat,
            pickupLng,
            excludeHailed: request.ScheduledAtUtc is null,
            cancellationToken);
        if (requireRider && rider is null)
        {
            return new PreparedBooking { Error = "No rider is available for that vehicle and payment method." };
        }

        if (hail.Rider is not null && requireHailReady)
        {
            var live = request.ScheduledAtUtc is null;
            var hailError = await ValidateHailBookingAsync(hail.Rider, request.PaymentMethod, live, cancellationToken);
            if (hailError is not null)
            {
                return new PreparedBooking { Error = hailError };
            }
        }

        return new PreparedBooking
        {
            Operator = op,
            Rider = rider,
            Pickup = pickup,
            Dropoff = dropoff,
            PickupDetails = pickupDetails,
            DropoffDetails = dropoffDetails,
            PickupLat = pickupLat,
            PickupLng = pickupLng,
            DropoffLat = dropoffLat,
            DropoffLng = dropoffLng,
            DistanceKm = distance,
            Fare = fare,
            EtaMinutes = eta,
            VehicleType = vehicle
        };
    }

    private async Task<RiderProfile?> PickRiderAsync(
        Guid operatorId,
        VehicleType vehicleType,
        PaymentMethod payment,
        double pickupLat,
        double pickupLng,
        bool excludeHailed,
        CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .Where(x => x.OperatorId == operatorId && x.IsActive && x.AppUser.IsActive && x.VehicleType == vehicleType)
            .ToListAsync(cancellationToken);
        riders = riders.Where(x => x.PaymentMethods.Any(m => m.Method == payment)).ToList();
        if (excludeHailed)
        {
            var hailed = await broadcast.LiveHailedRiderIdsAsync(cancellationToken);
            riders = riders.Where(x => !hailed.Contains(x.Id)).ToList();
        }
        if (riders.Count == 0)
        {
            return null;
        }

        var busy = await db.Trips
            .Where(x => x.OperatorId == operatorId && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .Select(x => x.RiderId)
            .ToListAsync(cancellationToken);
        var free = riders.Where(x => !busy.Contains(x.Id)).ToList();
        var pool = free.Count > 0 ? free : riders;
        return pool
            .OrderByDescending(x => x.IsOnline)
            .ThenByDescending(x => TripBroadcastService.CanReceiveBookings(x.Wallet?.Balance ?? 0))
            .ThenBy(x => Geo.DistanceKm(x.LastLat, x.LastLng, pickupLat, pickupLng) ?? double.MaxValue)
            .First();
    }

    private static CustomerBookRequest BindHail(CustomerProfile customer, CustomerBookRequest request)
    {
        if (request.ScheduledAtUtc is not null)
        {
            return request;
        }

        if (TripBroadcastService.HailIsLive(customer.HailAtUtc) && customer.HailRiderId is Guid hailed)
        {
            return request with { RiderId = hailed };
        }

        return request;
    }

    private async Task<(RiderProfile? Rider, string? Error)> ResolveHailRiderAsync(Guid? riderId, CancellationToken cancellationToken)
    {
        if (riderId is not Guid id || id == Guid.Empty)
        {
            return (null, null);
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AppUser.IsActive || rider.Operator is null || !rider.Operator.IsActive)
        {
            return (null, "This QR is not a Ya! Pasakay rider.");
        }

        return (rider, null);
    }

    private async Task<string?> ValidateHailBookingAsync(
        RiderProfile rider,
        PaymentMethod payment,
        bool live,
        CancellationToken cancellationToken)
    {
        if (!rider.PaymentMethods.Any(x => x.Method == payment))
        {
            return "This rider does not accept that payment method.";
        }

        if (!live)
        {
            return null;
        }

        if (!rider.IsOnline)
        {
            return "This rider is offline. Ask them to go online, then scan again.";
        }

        var busy = await db.Trips.AnyAsync(
            x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);
        return busy ? "This rider is on another trip right now." : null;
    }

    private Task<Barangay?> ResolveBarangayAsync(
        Guid? id,
        string details,
        CancellationToken cancellationToken) =>
        TerritoryLookup.MatchFromAddressAsync(db, id, details, cancellationToken);

    private sealed class PreparedBooking
    {
        public string? Error { get; set; }
        public Operator? Operator { get; set; }
        public RiderProfile? Rider { get; set; }
        public Barangay? Pickup { get; set; }
        public Barangay? Dropoff { get; set; }
        public string PickupDetails { get; set; } = string.Empty;
        public string DropoffDetails { get; set; } = string.Empty;
        public double PickupLat { get; set; }
        public double PickupLng { get; set; }
        public double DropoffLat { get; set; }
        public double DropoffLng { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal Fare { get; set; }
        public int EtaMinutes { get; set; }
        public VehicleType VehicleType { get; set; }
    }
}
