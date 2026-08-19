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
public class RiderDeskController(AppDbContext db, TripBroadcastService broadcast, RiderWalletService wallets, TripChatRealtime chatRealtime, LiveNotify live, UploadStore uploads) : ControllerBase
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

        var now = DateTime.UtcNow;
        offer.Trip.RiderId = rider.Id;
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
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (offer.Status == OfferStatus.Offered)
        {
            offer.Status = OfferStatus.Declined;
            offer.RespondedAtUtc = DateTime.UtcNow;
            offer.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
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

    [HttpGet("trips")]
    public async Task<ActionResult<IReadOnlyList<RideListItem>>> Trips(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var trips = await db.Trips
            .Where(x => x.RiderId == rider.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(200)
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
        return Ok(trips);
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
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
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
            rider.IsActive);
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
