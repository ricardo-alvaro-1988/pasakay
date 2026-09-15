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
[Authorize(Roles = "Customer")]
[Route("api/customer")]
public class CustomerBookingsController(
    AppDbContext db,
    TripBroadcastService broadcast,
    TripChatRealtime chatRealtime,
    LiveNotify live,
    GoogleDrivingDistance driving,
    UploadStore uploads,
    OperatorPromoService promos,
    DeriveFarePricingService deriveFares,
    IConfiguration config) : ControllerBase
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

    [HttpGet("services")]
    public async Task<ActionResult<CustomerServicesResponse>> Services(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] Guid? barangayId,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        Operator? op = null;
        Barangay? barangay = null;
        if (barangayId is Guid id)
        {
            barangay = await db.Barangays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        if (barangay is not null)
        {
            op = await db.Operators.AsNoTracking()
                .Where(x => x.IsActive && (
                    x.Areas.Any(a => a.BarangayId == barangay.Id)
                    || x.Areas.Any(a => a.Barangay.MunicipalityId == barangay.MunicipalityId)))
                .OrderBy(x => x.CompanyName)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (lat is double && lng is double)
        {
            op = await db.Operators.AsNoTracking()
                .Where(x => x.IsActive && x.Merchants.Any(m => m.IsActive))
                .OrderBy(x => x.CompanyName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Ok(new CustomerServicesResponse(op?.PabiliEnabled == true));
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

    [HttpGet("riders/available")]
    public async Task<ActionResult<IReadOnlyList<CustomerHailRider>>> AvailableRiders(
        [FromQuery] VehicleType vehicleType,
        [FromQuery] PaymentMethod paymentMethod,
        [FromQuery] double pickupLat,
        [FromQuery] double pickupLng,
        [FromQuery] Guid? pickupBarangayId,
        [FromQuery] string? pickupDetails,
        [FromQuery] Guid? vehicleCategoryId,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (pickupLat == 0 || pickupLng == 0)
        {
            return BadRequest(new { message = "Set pickup on the map first." });
        }

        if (VehicleTypeRules.ValidateChoice(vehicleType) is { } invalidVehicle)
        {
            return BadRequest(new { message = invalidVehicle });
        }

        var pickup = await ResolveBarangayAsync(pickupBarangayId, pickupDetails ?? string.Empty, cancellationToken);
        if (pickup is null)
        {
            return BadRequest(new { message = "Pickup must match a Philippine barangay." });
        }

        var op = await ResolveOperatorForBarangayAsync(pickup, cancellationToken);
        if (op is null)
        {
            return BadRequest(new { message = "No operator covers this pickup area yet." });
        }

        var riders = await broadcast.ListAvailableRidersAsync(
            op.Id,
            vehicleType,
            paymentMethod,
            pickupLat,
            pickupLng,
            pickup.Id,
            cancellationToken,
            vehicleCategoryId);
        return Ok(riders);
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
        return trip is null ? NotFound() : Ok(await OperatorMaps.RideDetailAsync(trip, db, cancellationToken));
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
        var prepared = await PrepareAsync(body, requireHailReady: false, requireRider: false, customer.Id, cancellationToken);
        if (prepared.Error is not null)
        {
            return BadRequest(new { message = prepared.Error });
        }

        var customerFare = prepared.CustomerFare;
        var hasPromos = await promos.HasOfferablePromosAsync(prepared.Operator!.Id, customer.Id, cancellationToken);
        return Ok(new CustomerQuoteResponse(
            customerFare,
            prepared.DistanceKm,
            prepared.EtaMinutes,
            prepared.Operator!.CompanyName,
            prepared.VehicleType,
            body.PaymentMethod,
            prepared.Rider is not null || prepared.Operator.BookingDispatchMode != BookingDispatchMode.Broadcast,
            prepared.Operator.BookingDispatchMode,
            prepared.MatrixFare,
            customerFare,
            prepared.PromoApplied,
            prepared.DiscountPercent,
            prepared.PromoDisplayCode,
            hasPromos,
            prepared.CustomerBoostAmount));
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
        // Vehicle availability is based on pickup municipality; drop-off is optional here.
        if (pickupDetails.Length == 0)
        {
            return Ok(new CustomerServiceCheckResponse(true, null, true, true));
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

        var coveringOps = await db.Operators
            .AsNoTracking()
            .Include(x => x.VehicleOffers!)
            .ThenInclude(o => o.VehicleCategory)
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.Barangay.MunicipalityId == municipalityId)))
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);
        if (coveringOps.Count == 0)
        {
            return Ok(new CustomerServiceCheckResponse(false, municipalityName));
        }

        var opIds = coveringOps.Select(x => x.Id).ToList();
        var offeredRows = await db.FareMatrices
            .AsNoTracking()
            .Where(x => opIds.Contains(x.OperatorId)
                && x.MunicipalityId == municipalityId
                && x.IsActive)
            .Select(x => new { x.OperatorId, x.VehicleType, x.VehicleCategoryId })
            .ToListAsync(cancellationToken);
        var offeredByOp = offeredRows
            .GroupBy(x => x.OperatorId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Types: g.Select(x => x.VehicleType).ToHashSet(),
                    Categories: g.Where(x => x.VehicleCategoryId is not null)
                        .Select(x => x.VehicleCategoryId!.Value)
                        .ToHashSet()));

        // Union catalog rows across covering operators so Sedan offered by any of them is visible.
        var vehiclesByCategory = new Dictionary<Guid, CustomerVehicleOfferDto>();
        foreach (var op in coveringOps)
        {
            offeredByOp.TryGetValue(op.Id, out var offered);
            var offeredTypes = offered.Types ?? [];
            var offeredCategories = offered.Categories ?? [];

            foreach (var o in op.VehicleOffers ?? [])
            {
                var cat = o.VehicleCategory;
                if (cat is null || !cat.IsActive)
                {
                    continue;
                }

                VehicleType type;
                if (cat.LegacyEnumValue is int v && Enum.IsDefined(typeof(VehicleType), v))
                {
                    type = (VehicleType)v;
                }
                else if (VehicleCatalog.TypeFor(cat.Id) is VehicleType platformType)
                {
                    type = platformType;
                }
                else
                {
                    type = VehicleType.Custom;
                }

                var hasFare = type == VehicleType.Custom
                    ? offeredCategories.Contains(cat.Id)
                    : offeredCategories.Contains(cat.Id) || offeredTypes.Contains(type);
                var available = o.IsEnabled && hasFare;

                if (vehiclesByCategory.TryGetValue(cat.Id, out var existing))
                {
                    if (!existing.Available && available)
                    {
                        vehiclesByCategory[cat.Id] = existing with { Available = true };
                    }

                    continue;
                }

                vehiclesByCategory[cat.Id] = new CustomerVehicleOfferDto(
                    cat.Id,
                    cat.Code,
                    o.DisplayName ?? cat.Name,
                    cat.IconKey,
                    o.MaxPassengers ?? cat.MaxPassengers,
                    cat.IsCargo,
                    available,
                    VehicleCatalog.ApiName(type));
            }
        }

        var vehicles = vehiclesByCategory.Values
            .OrderBy(v => VehicleCatalog.PresetFor(v.Id)?.SortOrder ?? 500)
            .ThenBy(v => v.Name)
            .ToList();

        // Legacy operators before offer seed: fall back to fare matrices only.
        if (vehicles.Count == 0)
        {
            var offeredSet = offeredRows.Select(x => x.VehicleType).ToHashSet();
            foreach (var preset in VehicleCatalog.PlatformPresets.Where(p => p.DefaultEnabled))
            {
                vehicles.Add(new CustomerVehicleOfferDto(
                    preset.Id,
                    preset.Code,
                    preset.Name,
                    preset.IconKey,
                    preset.MaxPassengers,
                    preset.IsCargo,
                    offeredSet.Contains(preset.LegacyEnum),
                    VehicleCatalog.ApiName(preset.LegacyEnum)));
            }
        }

        // Kill switch / empty bookable list: empty vehicles forces customer UI back to moto/trike bools.
        if (!config.GetValue("VehicleCatalog:UseOffersForCustomerUi", true)
            || vehicles.All(v => !v.Available))
        {
            vehicles.Clear();
        }

        var anyOfferedTypes = offeredRows.Select(x => x.VehicleType).ToHashSet();
        return Ok(new CustomerServiceCheckResponse(
            true,
            municipalityName,
            anyOfferedTypes.Contains(VehicleType.Motorcycle),
            anyOfferedTypes.Contains(VehicleType.Tricycle),
            vehicles));
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

        var isDirectHail = body.HailQr
            || (TripBroadcastService.HailIsLive(customer.HailAtUtc)
                && customer.HailRiderId is Guid hailedId
                && body.RiderId == hailedId);

        var preview = await PrepareAsync(
            body with { RiderId = isDirectHail ? body.RiderId : null },
            requireHailReady: false,
            requireRider: false,
            customer.Id,
            cancellationToken);
        if (preview.Error is not null || preview.Operator is null)
        {
            return BadRequest(new { message = preview.Error ?? "Could not create this booking." });
        }

        var mode = preview.Operator.BookingDispatchMode;
        var isScheduled = scheduled is not null;
        var customerPicksRider = !isDirectHail && !isScheduled && body.RiderId is Guid;
        if (!isDirectHail && !isScheduled)
        {
            if (mode == BookingDispatchMode.Selection && body.RiderId is null)
            {
                return BadRequest(new { message = "Choose a rider for this trip." });
            }

            if (mode == BookingDispatchMode.Broadcast && body.RiderId is Guid)
            {
                return BadRequest(new { message = "This operator broadcasts to nearby riders. Confirm without picking a rider." });
            }
        }

        var prepared = await PrepareAsync(
            body,
            requireHailReady: isDirectHail,
            requireRider: true,
            customer.Id,
            cancellationToken);
        if (prepared.Error is not null || prepared.Operator is null || prepared.Rider is null || prepared.Pickup is null || prepared.Dropoff is null)
        {
            return BadRequest(new { message = prepared.Error ?? "Could not create this booking." });
        }

        if (customerPicksRider)
        {
            var eligible = await broadcast.RankEligibleRidersAsync(
                prepared.Operator.Id,
                prepared.VehicleType,
                body.PaymentMethod,
                prepared.PickupLat,
                prepared.PickupLng,
                prepared.Pickup.Id,
                preferredRiderId: null,
                includePreferredEvenIfIneligible: false,
                cancellationToken,
                enforceRadius: false,
                vehicleCategoryId: prepared.VehicleCategoryId);
            if (eligible.All(x => x.Rider.Id != prepared.Rider.Id))
            {
                return BadRequest(new { message = "That rider is not available for this pickup right now." });
            }
        }

        var assignImmediately = isDirectHail;
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            OperatorId = prepared.Operator.Id,
            RiderId = prepared.Rider.Id,
            VehicleType = prepared.VehicleType,
            VehicleCategoryId = prepared.VehicleCategoryId
                ?? (VehicleTypeRules.IsKnown(prepared.VehicleType) ? VehicleCatalog.IdFor(prepared.VehicleType) : null),
            Status = assignImmediately ? TripStatus.Waiting : TripStatus.Pending,
            Pickup = prepared.PickupDetails,
            PickupDetails = prepared.PickupDetails,
            PickupBarangayId = prepared.Pickup.Id,
            PickupLat = prepared.PickupLat,
            PickupLng = prepared.PickupLng,
            Dropoff = prepared.DropoffDetails,
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
            Notes = isDirectHail
                ? TripBroadcastService.DirectHailNote
                : customerPicksRider
                    ? TripBroadcastService.CustomerPickNote
                    : string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            Fare = prepared.Fare,
            CustomerFare = prepared.CustomerFare,
            CustomerBoostAmount = prepared.CustomerBoostAmount,
            PromoDiscountAmount = prepared.PromoDiscountAmount,
            IsPromoSponsored = prepared.PromoApplied,
            PromoId = prepared.PromoId,
            DiscountPercent = prepared.DiscountPercent,
            DeriveFareZoneId = prepared.DeriveFareZoneId,
            DistanceKm = prepared.DistanceKm,
            PassengerCount = prepared.PassengerCount,
            PaymentMethod = body.PaymentMethod,
            PaymentMethodOther = body.PaymentMethod == PaymentMethod.Cash || string.IsNullOrWhiteSpace(body.PaymentMethodOther)
                ? null
                : body.PaymentMethodOther!.Trim(),
            RequestedAtUtc = now,
            ScheduledAtUtc = scheduled
        };
        db.Trips.Add(trip);
        if (assignImmediately)
        {
            if (isDirectHail)
            {
                customer.HailRiderId = null;
                customer.HailAtUtc = null;
            }

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
            await live.RiderAssignedAsync(prepared.Rider.Id, trip.Reference, isDirectHail, cancellationToken);
            await live.CustomerChangedAsync(customer.Id, isDirectHail ? "hail-booked" : "assigned", cancellationToken);
            return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
        }

        await db.SaveChangesAsync(cancellationToken);
        if (TripBroadcastService.IsDueForScheduleBroadcast(scheduled))
        {
            await broadcast.BroadcastAsync(trip.Id, cancellationToken);
        }

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
        trip.CancelledBy = CancelledBy.Customer;
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

    private async Task<PreparedBooking> PrepareAsync(
        CustomerBookRequest request,
        bool requireHailReady,
        bool requireRider,
        Guid? customerId,
        CancellationToken cancellationToken)
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
        Guid? vehicleCategoryId = hail.Rider?.VehicleCategoryId ?? request.VehicleCategoryId;
        var isCargo = VehicleTypeRules.IsCargo(vehicle);
        var maxPassengers = VehicleTypeRules.MaxPassengers(vehicle);

        if (vehicleCategoryId is Guid categoryId)
        {
            var cat = await db.VehicleCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == categoryId && x.IsActive, cancellationToken);
            if (cat is null)
            {
                return new PreparedBooking { Error = "That vehicle type is not available." };
            }

            vehicleCategoryId = cat.Id;
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

            isCargo = cat.IsCargo;
            maxPassengers = cat.MaxPassengers;
        }

        if (VehicleTypeRules.ValidateChoice(vehicle) is { } invalidVehicle)
        {
            return new PreparedBooking { Error = invalidVehicle };
        }

        var op = hail.Rider?.Operator;
        if (op is null)
        {
            op = await ResolveCoveringOperatorForVehicleAsync(
                pickup,
                vehicle,
                vehicleCategoryId,
                cancellationToken);
        }

        if (op is null)
        {
            return new PreparedBooking { Error = "No operator covers this pickup area yet." };
        }

        if (vehicleCategoryId is Guid resolvedCategoryId)
        {
            var offer = await db.OperatorVehicleOffers
                .AsNoTracking()
                .Include(x => x.VehicleCategory)
                .FirstOrDefaultAsync(
                    x => x.OperatorId == op.Id && x.VehicleCategoryId == resolvedCategoryId && x.IsEnabled,
                    cancellationToken);
            if (offer is null)
            {
                return new PreparedBooking { Error = "That vehicle type is not available." };
            }

            if (offer.VehicleCategory is not null)
            {
                maxPassengers = offer.MaxPassengers ?? offer.VehicleCategory.MaxPassengers;
                isCargo = offer.VehicleCategory.IsCargo;
            }
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
        var passengers = VehicleTypeRules.ClampPassengers(vehicle, isCargo, maxPassengers, request.PassengerCount);

        var fareRow = await OperatorMaps.LoadFareMatrixAsync(
            db,
            op.Id,
            vehicle,
            pickup.MunicipalityId,
            vehicleCategoryId,
            cancellationToken);
        if (fareRow is null)
        {
            return new PreparedBooking
            {
                Error = $"{VehicleTypeRules.Label(vehicle)} is not offered for bookings in this municipality."
            };
        }

        Guid? deriveZoneId = null;
        decimal fare;
        var derive = await deriveFares.ResolveAsync(op.Id, vehicle, pickupLat, pickupLng, distance, cancellationToken);
        if (derive.Error is not null)
        {
            return new PreparedBooking { Error = derive.Error };
        }

        if (derive.Zone is not null && derive.Matrix is not null)
        {
            deriveZoneId = derive.Zone.Id;
            fare = DeriveFarePricingService.ComputeWithSharedSurcharges(derive.Matrix, fareRow, passengers, distance);
        }
        else
        {
            fare = DeriveFarePricingService.ComputeMunicipalityWithSurcharges(fareRow, passengers, distance);
        }

        OperatorPromo? promo = null;
        var customerFare = fare;
        var promoDiscount = 0m;
        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            var resolved = await promos.ResolveForBookingAsync(op.Id, customerId, request.PromoCode, cancellationToken);
            if (resolved.Error is not null)
            {
                return new PreparedBooking { Error = resolved.Error };
            }

            promo = resolved.Promo;
            if (promo is not null)
            {
                (customerFare, promoDiscount) = OperatorPromoRules.SplitFare(fare, promo.DiscountPercent);
            }
        }

        var matrixFare = fare;
        var boost = NormalizeCustomerBoost(request.CustomerBoostAmount);
        if (boost > 0)
        {
            fare = CommissionCut.Round(fare + boost);
            customerFare = CommissionCut.Round(customerFare + boost);
        }

        var rider = hail.Rider ?? await PickRiderAsync(
            op.Id,
            vehicle,
            vehicleCategoryId,
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
            CustomerFare = customerFare,
            MatrixFare = matrixFare,
            CustomerBoostAmount = boost,
            PromoDiscountAmount = promoDiscount,
            PromoApplied = promo is not null,
            PromoId = promo?.Id,
            DiscountPercent = promo?.DiscountPercent,
            PromoDisplayCode = promo is not null ? OperatorPromoRules.DisplayCode(promo.DiscountPercent) : null,
            DeriveFareZoneId = deriveZoneId,
            PassengerCount = passengers,
            EtaMinutes = eta,
            VehicleType = vehicle,
            VehicleCategoryId = vehicleCategoryId
        };
    }

    private const decimal MaxCustomerBoostAmount = 500m;

    private static decimal NormalizeCustomerBoost(decimal amount)
    {
        if (amount <= 0)
        {
            return 0m;
        }

        var wholePesos = Math.Floor(amount);
        return wholePesos > MaxCustomerBoostAmount ? MaxCustomerBoostAmount : wholePesos;
    }

    private async Task<RiderProfile?> PickRiderAsync(
        Guid operatorId,
        VehicleType vehicleType,
        Guid? vehicleCategoryId,
        PaymentMethod payment,
        double pickupLat,
        double pickupLng,
        bool excludeHailed,
        CancellationToken cancellationToken)
    {
        IQueryable<RiderProfile> query = db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
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
        var funded = pool.Where(x => TripBroadcastService.CanReceiveBookings(x.Wallet?.Balance ?? 0)).ToList();
        if (funded.Count == 0)
        {
            return null;
        }

        return funded
            .OrderByDescending(x => x.IsOnline)
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

    /// <summary>
    /// Prefer a covering operator that has this vehicle enabled and an active fare in the pickup municipality.
    /// Falls back to the first covering operator (legacy behavior).
    /// </summary>
    private async Task<Operator?> ResolveCoveringOperatorForVehicleAsync(
        Barangay pickup,
        VehicleType vehicle,
        Guid? vehicleCategoryId,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Operators
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.BarangayId == pickup.Id)
                || x.Areas.Any(a => a.Barangay.MunicipalityId == pickup.MunicipalityId)))
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (vehicleCategoryId is Guid categoryId)
            {
                var enabled = await db.OperatorVehicleOffers.AsNoTracking().AnyAsync(
                    x => x.OperatorId == candidate.Id && x.VehicleCategoryId == categoryId && x.IsEnabled,
                    cancellationToken);
                if (!enabled)
                {
                    continue;
                }
            }

            var fare = await OperatorMaps.LoadFareMatrixAsync(
                db,
                candidate.Id,
                vehicle,
                pickup.MunicipalityId,
                vehicleCategoryId,
                cancellationToken);
            if (fare is not null)
            {
                return candidate;
            }
        }

        return candidates[0];
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
            .Include(x => x.Wallet)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AcceptsPasakay || !rider.AppUser.IsActive || rider.Operator is null || !rider.Operator.IsActive)
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

        var balance = rider.Wallet?.Balance ?? 0;
        if (!TripBroadcastService.CanReceiveBookings(balance))
        {
            return TripBroadcastService.WalletBlockedMessage(balance);
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

    private Task<Operator?> ResolveOperatorForBarangayAsync(Barangay pickup, CancellationToken cancellationToken) =>
        db.Operators
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.BarangayId == pickup.Id)
                || x.Areas.Any(a => a.Barangay.MunicipalityId == pickup.MunicipalityId)))
            .OrderBy(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

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
        public decimal CustomerFare { get; set; }
        public decimal MatrixFare { get; set; }
        public decimal CustomerBoostAmount { get; set; }
        public decimal PromoDiscountAmount { get; set; }
        public bool PromoApplied { get; set; }
        public Guid? PromoId { get; set; }
        public int? DiscountPercent { get; set; }
        public string? PromoDisplayCode { get; set; }
        public Guid? DeriveFareZoneId { get; set; }
        public int PassengerCount { get; set; } = 1;
        public int EtaMinutes { get; set; }
        public VehicleType VehicleType { get; set; }
        public Guid? VehicleCategoryId { get; set; }
    }
}
