using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Rider")]
[Route("api/rider")]
public class RiderDeskController(
    AppDbContext db,
    TripBroadcastService broadcast,
    RiderWalletService wallets,
    TripChatRealtime chatRealtime,
    LiveNotify live,
    UploadStore uploads,
    OperatorPromoService promos,
    IConfiguration config) : ControllerBase
{
    [HttpGet("desk")]
    public async Task<ActionResult<RiderDeskResponse>> Desk(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        await broadcast.ExpireStaleAsync(null, cancellationToken);
        if (rider.IsOnline)
        {
            await broadcast.BroadcastPendingForOperatorAsync(rider.OperatorId, cancellationToken);
        }

        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpGet("earnings-summary")]
    public async Task<ActionResult<RiderEarningsSummaryResponse>> EarningsSummary(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var dailyGoal = config.GetValue("Rider:DailyEarningsGoal", 1000m);
        if (dailyGoal <= 0)
        {
            dailyGoal = 1000m;
        }

        var todayPh = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var weekStart = todayPh.AddDays(-(int)todayPh.DayOfWeek); // Sunday start PH
        var monthStart = new DateOnly(todayPh.Year, todayPh.Month, 1);

        var monthStartUtc = PhDayStartUtc(monthStart);
        var todayEndExclusiveUtc = PhDayStartUtc(todayPh.AddDays(1));

        var trips = await db.Trips
            .AsNoTracking()
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .Where(x => x.RiderId == rider.Id
                && x.Status == TripStatus.Completed
                && x.Fare > 0
                && (x.CompletedAtUtc ?? x.RequestedAtUtc) >= monthStartUtc
                && (x.CompletedAtUtc ?? x.RequestedAtUtc) < todayEndExclusiveUtc)
            .ToListAsync(cancellationToken);

        var fares = await OperatorMaps.LoadFareMatrixLookupAsync(db, trips, cancellationToken);

        decimal SumDriver(IEnumerable<Trip> rows)
        {
            decimal total = 0;
            foreach (var trip in rows)
            {
                FareMatrix? fare = null;
                if (trip.PickupBarangay is not null)
                {
                    fares.TryGetValue((trip.OperatorId, trip.VehicleType, trip.PickupBarangay.MunicipalityId), out fare);
                }

                var breakdown = RideCommissionCalculator.ForTrip(trip, fare);
                if (breakdown is not null)
                {
                    total += breakdown.DriverAmount;
                }
            }

            return CommissionCut.Round(total);
        }

        bool InRange(Trip trip, DateOnly from, DateOnly toInclusive)
        {
            var stamp = trip.CompletedAtUtc ?? trip.RequestedAtUtc;
            var day = DateOnly.FromDateTime(stamp.AddHours(8));
            return day >= from && day <= toInclusive;
        }

        var todayTrips = trips.Where(x => InRange(x, todayPh, todayPh)).ToList();
        var weekTrips = trips.Where(x => InRange(x, weekStart, todayPh)).ToList();
        var monthTrips = trips;

        var todayEarnings = SumDriver(todayTrips);
        var weekEarnings = SumDriver(weekTrips);
        var monthEarnings = SumDriver(monthTrips);
        var avgPerTrip = todayTrips.Count == 0
            ? 0
            : CommissionCut.Round(todayEarnings / todayTrips.Count);
        var goalProgress = dailyGoal <= 0 ? 0 : Math.Min(1m, todayEarnings / dailyGoal);
        int? tripsToGoal = null;
        if (todayEarnings < dailyGoal && avgPerTrip > 0)
        {
            tripsToGoal = (int)Math.Ceiling((double)((dailyGoal - todayEarnings) / avgPerTrip));
        }
        else if (todayEarnings < dailyGoal && todayTrips.Count == 0)
        {
            tripsToGoal = null;
        }

        double? averageRating = null;
        var rated = await db.Trips
            .AsNoTracking()
            .Where(x => x.RiderId == rider.Id && x.Rating != null && x.Rating > 0)
            .Select(x => x.Rating!.Value)
            .ToListAsync(cancellationToken);
        if (rated.Count > 0)
        {
            averageRating = Math.Round(rated.Average(), 1);
        }

        return Ok(new RiderEarningsSummaryResponse(
            todayEarnings,
            todayTrips.Count,
            avgPerTrip,
            weekEarnings,
            weekTrips.Count,
            monthEarnings,
            monthTrips.Count,
            CommissionCut.Round(dailyGoal),
            goalProgress,
            tripsToGoal,
            averageRating));
    }

    private static DateTime PhDayStartUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);

    [HttpGet("trips")]
    public async Task<ActionResult<IReadOnlyList<RideListItem>>> Trips(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var tripRows = await db.Trips
            .AsNoTracking()
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .Where(x => x.RiderId == rider.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var fares = await OperatorMaps.LoadFareMatrixLookupAsync(db, tripRows, cancellationToken);
        var trips = tripRows.Select(x =>
        {
            FareMatrix? fare = null;
            if (x.PickupBarangay is not null)
            {
                fares.TryGetValue((x.OperatorId, x.VehicleType, x.PickupBarangay.MunicipalityId), out fare);
            }

            return new RideListItem(
                x.Id,
                x.Reference,
                DateTime.SpecifyKind(x.RequestedAtUtc, DateTimeKind.Utc),
                TripAddress.Display(x.PickupDetails, x.Pickup),
                TripAddress.Display(x.DropoffDetails, x.Dropoff),
                x.CustomerName,
                x.VehicleType,
                x.Status,
                x.Fare,
                x.DistanceKm,
                Math.Max(1, x.PassengerCount),
                x.PaymentMethod,
                x.PaymentMethodOther,
                RideCommissionCalculator.ForTrip(x, fare),
                x.CustomerFare > 0 ? x.CustomerFare : x.Fare,
                x.PromoDiscountAmount,
                x.IsPromoSponsored,
                x.DiscountPercent,
                x.IsPromoSponsored && x.DiscountPercent is int pct ? $"Save{pct}" : null,
                x.CustomerBoostAmount);
        }).ToList();

        return Ok(trips.Select(x => x with
        {
            RequestedAtUtc = RiderDisplayTime.ToApi(x.RequestedAtUtc),
            Pickup = TripAddress.Clean(x.Pickup),
            Dropoff = TripAddress.Clean(x.Dropoff)
        }).ToList());
    }

    [HttpPost("online")]
    public async Task<ActionResult<RiderDeskResponse>> SetOnline(
        [FromBody] RiderOnlineBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        rider.IsOnline = request.Online;
        rider.OnlineAtUtc = request.Online ? DateTime.UtcNow : rider.OnlineAtUtc;
        rider.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (request.Online)
        {
            await broadcast.BroadcastPendingForOperatorAsync(rider.OperatorId, cancellationToken);
        }
        else
        {
            await ExpireRiderOffersAsync(rider.Id, cancellationToken);
            await ClearRiderHailsAsync(rider.Id, cancellationToken);
        }

        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] RiderPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        if (!SecretHasher.Verify(request.CurrentPassword ?? string.Empty, rider.AppUser.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (!SecretHasher.IsStrongPassword(request.NewPassword))
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        rider.AppUser.PasswordHash = SecretHasher.Hash(request.NewPassword.Trim());
        rider.AppUser.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password updated." });
    }

    [HttpPost("payments")]
    public async Task<ActionResult<RiderDeskResponse>> SetPayments(
        [FromBody] RiderPaymentsBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var paymentSync = await RiderPaymentSync.SyncAsync(db, rider, request.PaymentMethods, cancellationToken);
        if (!paymentSync.Ok)
        {
            return BadRequest(new { message = paymentSync.Error });
        }

        rider.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await live.RiderChangedAsync(rider.Id, "payments", cancellationToken);
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("location")]
    public async Task<ActionResult<RiderDeskResponse>> Location(
        [FromBody] RiderLocationBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        if (request.Lat is < -90 or > 90 || request.Lng is < -180 or > 180)
        {
            return BadRequest(new { message = "Invalid location." });
        }

        rider.LastLat = request.Lat;
        rider.LastLng = request.Lng;
        rider.LastLocationAtUtc = DateTime.UtcNow;
        rider.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("hail")]
    public async Task<ActionResult<RiderDeskResponse>> Hail(
        [FromBody] RiderHailBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        if (!rider.IsOnline)
        {
            return BadRequest(new { message = "Go online first, then scan the customer QR." });
        }

        var busy = await db.Trips.AnyAsync(
            x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);
        if (busy)
        {
            return BadRequest(new { message = "Finish your current trip first." });
        }

        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (customer is null || !customer.AppUser.IsActive)
        {
            return NotFound(new { message = "This QR is not a Ya! Pasakay customer." });
        }

        var customerBusy = await db.Trips.AnyAsync(
            x => x.CustomerId == customer.Id
                && (x.Status == TripStatus.Pending || x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing)
                && (x.ScheduledAtUtc == null || x.ScheduledAtUtc <= DateTime.UtcNow),
            cancellationToken);
        if (customerBusy)
        {
            return BadRequest(new { message = "This customer already has an active booking." });
        }

        var previous = await db.CustomerProfiles
            .Where(x => x.HailRiderId == rider.Id && x.Id != customer.Id)
            .ToListAsync(cancellationToken);
        foreach (var row in previous)
        {
            row.HailRiderId = null;
            row.HailAtUtc = null;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        customer.HailRiderId = rider.Id;
        customer.HailAtUtc = DateTime.UtcNow;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await live.CustomerTripAsync(
            customer.Id,
            "hail",
            "Rider ready",
            $"{rider.AppUser.FullName} scanned your QR. Set pickup and book.",
            cancellationToken);
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("hail/cancel")]
    public async Task<ActionResult<RiderDeskResponse>> CancelHail(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        await ClearRiderHailsAsync(rider.Id, cancellationToken);
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("offers/{id:guid}/accept")]
    public async Task<ActionResult<RiderDeskResponse>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var offer = await db.TripOffers
            .Include(x => x.Trip)
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (offer.Status != OfferStatus.Offered || offer.ExpiresAtUtc < DateTime.UtcNow)
        {
            return BadRequest(new { message = "This job offer expired." });
        }

        if (offer.Trip.Status != TripStatus.Pending)
        {
            return BadRequest(new { message = "Another rider already took this job." });
        }

        if (offer.Trip.OperatorId != rider.OperatorId)
        {
            return BadRequest(new { message = "This booking belongs to another operator." });
        }

        var coverage = await OperatorAreaSync.CoverageErrorAsync(
            db,
            rider.OperatorId,
            offer.Trip.PickupBarangayId,
            cancellationToken);
        if (coverage is not null && offer.Trip.RiderId != rider.Id)
        {
            return BadRequest(new { message = coverage });
        }

        var busy = await db.Trips.AnyAsync(
            x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);
        if (busy)
        {
            return BadRequest(new { message = "Finish your current trip first." });
        }

        var balance = rider.Wallet?.Balance ?? 0;
        if (!TripBroadcastService.CanReceiveBookings(balance))
        {
            return BadRequest(new { message = TripBroadcastService.WalletBlockedMessage(balance) });
        }

        var now = DateTime.UtcNow;
        offer.Trip.RiderId = rider.Id;
        offer.Trip.VehicleType = rider.VehicleType;
        offer.Trip.Status = TripStatus.Waiting;
        offer.Trip.UpdatedAtUtc = now;
        offer.Status = OfferStatus.Accepted;
        offer.RespondedAtUtc = now;
        offer.UpdatedAtUtc = now;

        var others = await db.TripOffers
            .Where(x => x.TripId == offer.TripId && x.Id != offer.Id && x.Status == OfferStatus.Offered)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.Status = OfferStatus.Expired;
            other.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await live.TripPartiesAsync(offer.Trip, "accepted", cancellationToken);
        if (offer.Trip.CustomerId is Guid customerId)
        {
            await live.CustomerTripAsync(
                customerId,
                "accepted",
                "Rider assigned",
                $"{rider.AppUser.FullName} accepted your trip {offer.Trip.Reference}.",
                cancellationToken);
        }
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("offers/{id:guid}/decline")]
    public async Task<ActionResult<RiderDeskResponse>> Decline(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var offer = await db.TripOffers
            .Include(x => x.Trip)
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (offer.Status == OfferStatus.Offered)
        {
            var now = DateTime.UtcNow;
            offer.Status = OfferStatus.Declined;
            offer.RespondedAtUtc = now;
            offer.UpdatedAtUtc = now;
            if (TripBroadcastService.IsCustomerPick(offer.Trip.Notes) && offer.Trip.Status == TripStatus.Pending)
            {
                offer.Trip.Status = TripStatus.Cancelled;
                offer.Trip.CancelledAtUtc = now;
                offer.Trip.CancelReason = "Rider declined the booking.";
                offer.Trip.CancelledBy = CancelledBy.Rider;
                offer.Trip.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (offer.Trip.Status == TripStatus.Cancelled && offer.Trip.CustomerId is Guid customerId)
            {
                await live.CustomerTripAsync(
                    customerId,
                    "cancelled",
                    "Rider declined",
                    $"No rider accepted {offer.Trip.Reference}. Book again to pick another rider.",
                    cancellationToken);
            }
        }

        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("trips/{id:guid}/start")]
    public async Task<ActionResult<RiderDeskResponse>> Start(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status != TripStatus.Waiting)
        {
            return BadRequest(new { message = "Start the trip after you accept and arrive at pickup." });
        }

        trip.Status = TripStatus.Ongoing;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (trip.CustomerId is Guid customerId)
        {
            await live.CustomerTripAsync(customerId, "started", "Trip started", $"Your trip {trip.Reference} is ongoing.", cancellationToken);
        }
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpGet("trips/{id:guid}/chat")]
    public async Task<ActionResult<IReadOnlyList<RideChatMessageItem>>> Chat(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
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

    [HttpPost("trips/{id:guid}/chat")]
    public async Task<ActionResult<RideChatMessageItem>> SendChat(
        Guid id,
        [FromBody] TripChatSendRequest request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var sent = await TripChatService.SendAsync(db, trip, ChatSender.Rider, request.Body, null, cancellationToken);
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
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var saved = await TripChatService.SavePhotoAsync(uploads, photo, trip.Id, cancellationToken);
        if (saved.Path is null)
        {
            return BadRequest(new { message = saved.Error });
        }

        var sent = await TripChatService.SendAsync(db, trip, ChatSender.Rider, body, saved.Path, cancellationToken);
        if (sent.Message is null)
        {
            return BadRequest(new { message = sent.Error });
        }

        await chatRealtime.BroadcastAsync(trip, sent.Message, cancellationToken);
        await live.ChatMessageAsync(trip, sent.Message, cancellationToken);
        return Ok(sent.Message);
    }

    [HttpGet("trips/{id:guid}")]
    public async Task<ActionResult<RideDetailResponse>> TripDetail(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var detail = await OperatorMaps.RideDetailAsync(trip, db, cancellationToken);
        return Ok(detail with
        {
            RequestedAtUtc = RiderDisplayTime.PrimaryTripAt(detail.RequestedAtUtc, detail.ScheduledAtUtc),
            ScheduledAtUtc = RiderDisplayTime.ToApi(detail.ScheduledAtUtc),
            CompletedAtUtc = RiderDisplayTime.ToApi(detail.CompletedAtUtc),
            CancelledAtUtc = RiderDisplayTime.ToApi(detail.CancelledAtUtc),
            RatedAtUtc = RiderDisplayTime.ToApi(detail.RatedAtUtc),
            Chat = detail.Chat
                .Select(m => m with { SentAtUtc = RiderDisplayTime.ToApi(m.SentAtUtc) })
                .ToList()
        });
    }

    [HttpPost("trips/{id:guid}/complete")]
    public async Task<ActionResult<RiderDeskResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            return BadRequest(new { message = "This trip is already finished." });
        }

        if (trip.Status != TripStatus.Ongoing)
        {
            return BadRequest(new { message = "Start the trip before completing it." });
        }

        trip.Status = TripStatus.Completed;
        trip.CompletedAtUtc = DateTime.UtcNow;
        trip.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await wallets.ApplyCommissionAsync(trip, cancellationToken);
        await promos.RedeemOnCompleteAsync(trip, cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        if (trip.CustomerId is Guid customerId)
        {
            await live.CustomerTripAsync(
                customerId,
                "completed",
                "Trip completed",
                $"Rate your ride {trip.Reference} in Booking.",
                cancellationToken);
        }
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    [HttpPost("trips/{id:guid}/cancel")]
    public async Task<ActionResult<RiderDeskResponse>> CancelTrip(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Ongoing)
        {
            return BadRequest(new { message = "This trip can no longer be cancelled." });
        }

        if (trip.Status is not (TripStatus.Pending or TripStatus.Waiting))
        {
            return BadRequest(new { message = "This trip can no longer be cancelled." });
        }

        trip.Status = TripStatus.Cancelled;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelReason = "Rider cancelled the booking.";
        trip.CancelledBy = CancelledBy.Rider;
        trip.UpdatedAtUtc = DateTime.UtcNow;

        var profile = await db.RiderProfiles.FirstAsync(x => x.Id == rider.Id, cancellationToken);
        profile.RiderCancelCount += 1;
        profile.CredibilityScore = Math.Max(0, profile.CredibilityScore - 5);
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await broadcast.ExpireTripAsync(trip.Id, cancellationToken);
        await live.TripPartiesAsync(trip, "cancelled", cancellationToken);
        return Ok(await BuildDeskAsync(rider.Id, cancellationToken));
    }

    private async Task<RiderDeskResponse> BuildDeskAsync(Guid riderId, CancellationToken cancellationToken)
    {
        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .FirstAsync(x => x.Id == riderId, cancellationToken);

        var balance = rider.Wallet?.Balance ?? 0;
        var canReceive = TripBroadcastService.CanReceiveBookings(balance);
        var now = DateTime.UtcNow;

        var active = await db.Trips
            .Where(x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        int previousBookingCount = 0;
        int completedBookingCount = 0;
        int cancelledBookingCount = 0;
        DateTime? lastCompletedAtUtc = null;
        if (active?.CustomerId is Guid customerId)
        {
            var history = await db.Trips
                .Where(x => x.CustomerId == customerId && x.Id != active.Id)
                .Select(x => new { x.Status, x.CompletedAtUtc })
                .ToListAsync(cancellationToken);
            previousBookingCount = history.Count;
            completedBookingCount = history.Count(x => x.Status == TripStatus.Completed);
            cancelledBookingCount = history.Count(x => x.Status == TripStatus.Cancelled);
            lastCompletedAtUtc = history
                .Where(x => x.Status == TripStatus.Completed && x.CompletedAtUtc != null)
                .OrderByDescending(x => x.CompletedAtUtc)
                .Select(x => x.CompletedAtUtc)
                .FirstOrDefault();
        }

        var offers = Array.Empty<RiderOfferItem>();
        if (active is null && rider.IsOnline)
        {
            var rows = await db.TripOffers
                .Include(x => x.Trip)
                .Where(x => x.RiderId == rider.Id
                    && x.Status == OfferStatus.Offered
                    && x.ExpiresAtUtc >= now
                    && x.Trip.Status == TripStatus.Pending)
                .OrderByDescending(x => x.IsPreferred)
                .ThenBy(x => x.DistanceKm ?? 999)
                .ToListAsync(cancellationToken);
            offers = rows.Select(x => TripBroadcastService.MapOffer(x, x.Trip)).ToArray();
        }

        RiderPendingHail? pendingHail = null;
        if (active is null)
        {
            var hail = await db.CustomerProfiles
                .Include(x => x.AppUser)
                .Where(x => x.HailRiderId == rider.Id)
                .OrderByDescending(x => x.HailAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (hail is not null && TripBroadcastService.HailIsLive(hail.HailAtUtc))
            {
                pendingHail = new RiderPendingHail(
                    hail.Id,
                    hail.DisplayName,
                    hail.AppUser.PhoneNumber,
                    DateTime.SpecifyKind(hail.HailAtUtc!.Value, DateTimeKind.Utc));
            }
        }

        return new RiderDeskResponse(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.PlateNumber,
            rider.VehicleType,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            rider.Operator.CompanyName,
            rider.IsOnline,
            balance,
            TripBroadcastService.MinWalletToReceive,
            canReceive && rider.IsOnline,
            !canReceive,
            TripBroadcastService.WalletHighlight(balance, canReceive),
            rider.PaymentMethods.Select(x => x.Method).OrderBy(x => x).ToList(),
            active is null ? null : TripBroadcastService.MapTrip(
                active,
                previousBookingCount,
                completedBookingCount,
                cancelledBookingCount,
                lastCompletedAtUtc),
            offers,
            pendingHail,
            rider.VehicleModel,
            rider.LicenseType,
            rider.LicenseNumber,
            UploadUrls.FromPath(rider.LicensePhotoPath),
            rider.FullAddress,
            rider.IsActive,
            rider.CredibilityScore,
            rider.RiderCancelCount);
    }

    private async Task ClearRiderHailsAsync(Guid riderId, CancellationToken cancellationToken)
    {
        var rows = await db.CustomerProfiles
            .Where(x => x.HailRiderId == riderId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.HailRiderId = null;
            row.HailAtUtc = null;
            row.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireRiderOffersAsync(Guid riderId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var offers = await db.TripOffers
            .Where(x => x.RiderId == riderId && x.Status == OfferStatus.Offered)
            .ToListAsync(cancellationToken);
        foreach (var offer in offers)
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = now;
        }

        if (offers.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
