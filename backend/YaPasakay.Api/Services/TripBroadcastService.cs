using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class TripBroadcastService(AppDbContext db, LiveNotify live)
{
    public const decimal MinWalletToReceive = 100m;
    /// <summary>Default broadcast radius when operator has not set one (km).</summary>
    public const double DefaultBroadcastRadiusKm = 5;
    public const double MinBroadcastRadiusKm = 1;
    public const double MaxBroadcastRadiusKm = 50;
    public const int MaxRiders = 20;
    public const string DirectHailNote = "Direct hail";
    public const string CustomerPickNote = "Customer selected rider";
    public static readonly TimeSpan HailTtl = TimeSpan.FromMinutes(10);

    public static bool HailIsLive(DateTime? at) =>
        at is DateTime stamped && DateTime.UtcNow - stamped <= HailTtl;

    public static bool IsCustomerPick(string? notes) =>
        string.Equals(notes, CustomerPickNote, StringComparison.OrdinalIgnoreCase);
    public static readonly TimeSpan LiveOfferTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ScheduledOfferTtl = TimeSpan.FromHours(2);
    /// <summary>Offer scheduled trips to riders only within this lead before pickup.</summary>
    public static readonly TimeSpan ScheduleBroadcastLead = TimeSpan.FromMinutes(60);

    /// <summary>Live trips always broadcast; scheduled only when within lead (or overdue Pending).</summary>
    public static bool IsDueForScheduleBroadcast(DateTime? scheduledAtUtc, DateTime? utcNow = null)
    {
        if (scheduledAtUtc is not DateTime scheduled)
        {
            return true;
        }

        var now = utcNow ?? DateTime.UtcNow;
        return scheduled <= now.Add(ScheduleBroadcastLead);
    }

    /// <summary>Riders need more than ₱100 to receive bookings (₱100 or less is blocked).</summary>
    public static bool CanReceiveBookings(decimal balance) => balance > MinWalletToReceive;

    public static string WalletBlockedMessage(decimal balance) =>
        $"Wallet must be above ₱{MinWalletToReceive:0} to receive bookings. Balance: ₱{balance:0.00}.";

    public static string WalletHighlight(decimal balance, bool canReceive) =>
        canReceive
            ? $"Keep more than ₱{MinWalletToReceive:0} in your wallet to receive bookings. Balance: ₱{balance:0.00}."
            : WalletBlockedMessage(balance);

    public async Task BroadcastAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null || trip.Status != TripStatus.Pending)
        {
            return;
        }

        if (!IsDueForScheduleBroadcast(trip.ScheduledAtUtc))
        {
            return;
        }

        await ExpireStaleAsync(trip.Id, cancellationToken);

        if (string.Equals(trip.Notes, DirectHailNote, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Broadcast must NOT treat the provisional Trip.RiderId as preferred — that caused
        // only the pre-picked phone to get the job while other nearby riders were skipped.
        // Customer-pick keeps a single preferred rider.
        var customerPick = IsCustomerPick(trip.Notes);
        Guid? preferredRiderId = customerPick ? trip.RiderId : null;

        var radiusKm = await ResolveBroadcastRadiusKmAsync(trip.OperatorId, cancellationToken);

        var ranked = await RankEligibleRidersAsync(
            trip.OperatorId,
            trip.VehicleType,
            trip.PaymentMethod,
            trip.PickupLat,
            trip.PickupLng,
            trip.PickupBarangayId,
            preferredRiderId,
            includePreferredEvenIfIneligible: customerPick,
            cancellationToken,
            enforceRadius: true,
            radiusKm: radiusKm);

        var existing = await db.TripOffers
            .Where(x => x.TripId == trip.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var expires = trip.ScheduledAtUtc is DateTime scheduled && scheduled > now.AddMinutes(15)
            ? now.Add(ScheduledOfferTtl)
            : now.Add(LiveOfferTtl);

        var chosen = customerPick
            ? ranked.Where(x => x.Rider.Id == trip.RiderId).Take(1).ToList()
            : ranked.Take(MaxRiders).ToList();

        var notifyRiderIds = new HashSet<Guid>();
        foreach (var (rider, distance, preferred) in chosen)
        {
            var offer = existing.FirstOrDefault(x => x.RiderId == rider.Id);
            if (offer is null)
            {
                db.TripOffers.Add(new TripOffer
                {
                    TripId = trip.Id,
                    RiderId = rider.Id,
                    Status = OfferStatus.Offered,
                    IsPreferred = preferred,
                    DistanceKm = distance is double km ? Math.Round((decimal)km, 2) : null,
                    OfferedAtUtc = now,
                    ExpiresAtUtc = expires
                });
                notifyRiderIds.Add(rider.Id);
                continue;
            }

            if (offer.Status == OfferStatus.Declined || offer.Status == OfferStatus.Accepted)
            {
                offer.IsPreferred = preferred;
                continue;
            }

            if (offer.Status == OfferStatus.Offered && offer.ExpiresAtUtc > now.AddMinutes(1))
            {
                offer.IsPreferred = preferred;
                if (distance is double liveKm)
                {
                    offer.DistanceKm = Math.Round((decimal)liveKm, 2);
                }

                // Still ping so every nearby rider gets the broadcast (SignalR / push).
                notifyRiderIds.Add(rider.Id);
                continue;
            }

            offer.Status = OfferStatus.Offered;
            offer.IsPreferred = preferred;
            offer.DistanceKm = distance is double refreshKm ? Math.Round((decimal)refreshKm, 2) : offer.DistanceKm;
            offer.OfferedAtUtc = now;
            offer.ExpiresAtUtc = expires;
            offer.RespondedAtUtc = null;
            offer.UpdatedAtUtc = now;
            notifyRiderIds.Add(rider.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var riderId in notifyRiderIds)
        {
            await live.RiderOfferAsync(riderId, trip.Reference, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<(RiderProfile Rider, double? Distance, bool Preferred)>> RankEligibleRidersAsync(
        Guid operatorId,
        VehicleType vehicleType,
        PaymentMethod paymentMethod,
        double? pickupLat,
        double? pickupLng,
        Guid? pickupBarangayId,
        Guid? preferredRiderId,
        bool includePreferredEvenIfIneligible,
        CancellationToken cancellationToken,
        bool enforceRadius = true,
        double? radiusKm = null)
    {
        var effectiveRadius = ClampRadius(radiusKm ?? await ResolveBroadcastRadiusKmAsync(operatorId, cancellationToken));

        var outsideCoverage = await OperatorAreaSync.CoverageErrorAsync(
                db,
                operatorId,
                pickupBarangayId,
                cancellationToken) is not null;

        var busy = await db.Trips
            .Where(x => x.OperatorId == operatorId
                && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .Select(x => x.RiderId)
            .ToListAsync(cancellationToken);
        var hailed = await LiveHailedRiderIdsAsync(cancellationToken);

        var riders = await db.RiderProfiles
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Where(x => x.OperatorId == operatorId
                && x.IsActive
                && x.IsOnline
                && x.AppUser.IsActive)
            .ToListAsync(cancellationToken);

        var ranked = new List<(RiderProfile Rider, double? Distance, bool Preferred)>();
        foreach (var rider in riders)
        {
            var preferred = preferredRiderId is Guid pref && rider.Id == pref;
            if (outsideCoverage && !preferred)
            {
                continue;
            }

            if ((busy.Contains(rider.Id) || hailed.Contains(rider.Id)) && !preferred)
            {
                continue;
            }

            if (rider.VehicleType != vehicleType)
            {
                continue;
            }

            if (!rider.PaymentMethods.Any(x => x.Method == paymentMethod))
            {
                continue;
            }

            var balance = rider.Wallet?.Balance ?? 0;
            // Wallet balance minimum should apply consistently (including preferred rider overrides).
            if (!CanReceiveBookings(balance))
            {
                continue;
            }

            var distance = Geo.DistanceKm(rider.LastLat, rider.LastLng, pickupLat, pickupLng);
            // Prefer known nearby riders; still include online riders with no GPS so they
            // are not silently dropped from broadcast.
            if (!preferred
                && pickupLat is not null
                && pickupLng is not null
                && distance is double km
                && enforceRadius
                && km > effectiveRadius)
            {
                continue;
            }

            ranked.Add((rider, distance, preferred));
        }

        var ordered = ranked
            .OrderByDescending(x => x.Preferred)
            .ThenBy(x => x.Distance ?? double.MaxValue)
            .ToList();

        if (includePreferredEvenIfIneligible
            && preferredRiderId is Guid preferredId
            && ordered.All(x => !x.Preferred))
        {
            var preferredRider = riders.FirstOrDefault(x => x.Id == preferredId)
                ?? await db.RiderProfiles
                    .Include(x => x.Wallet)
                    .Include(x => x.PaymentMethods)
                    .Include(x => x.AppUser)
                    .Include(x => x.Operator)
                    .FirstOrDefaultAsync(x => x.Id == preferredId && x.OperatorId == operatorId, cancellationToken);
            // Never bypass the wallet floor — low-balance riders must not receive offers.
            if (preferredRider is not null
                && CanReceiveBookings(preferredRider.Wallet?.Balance ?? 0)
                && ordered.Count < MaxRiders)
            {
                var distance = Geo.DistanceKm(preferredRider.LastLat, preferredRider.LastLng, pickupLat, pickupLng);
                ordered.Insert(0, (preferredRider, distance, true));
            }
        }

        return ordered;
    }

    public async Task<IReadOnlyList<CustomerHailRider>> ListAvailableRidersAsync(
        Guid operatorId,
        VehicleType vehicleType,
        PaymentMethod paymentMethod,
        double pickupLat,
        double pickupLng,
        Guid? pickupBarangayId,
        CancellationToken cancellationToken)
    {
        var ranked = await RankEligibleRidersAsync(
            operatorId,
            vehicleType,
            paymentMethod,
            pickupLat,
            pickupLng,
            pickupBarangayId,
            preferredRiderId: null,
            includePreferredEvenIfIneligible: false,
            cancellationToken,
            enforceRadius: false);

        return ranked
            .Take(MaxRiders)
            .Select(x => MapAvailableRider(x.Rider, x.Distance, busy: false))
            .ToList();
    }

    public static CustomerHailRider MapAvailableRider(RiderProfile rider, double? distanceKm, bool busy) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.PlateNumber,
            rider.VehicleType,
            rider.VehicleModel,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            rider.AppUser.PhoneNumber,
            rider.IsOnline,
            busy,
            rider.Operator?.CompanyName ?? string.Empty,
            rider.PaymentMethods.Select(x => x.Method).Distinct().OrderBy(x => x).ToList(),
            distanceKm is double km ? Math.Round(km, 2) : null);
    public async Task BroadcastPendingForOperatorAsync(Guid operatorId, CancellationToken cancellationToken)
    {
        var tripIds = await db.Trips
            .Where(x => x.OperatorId == operatorId && x.Status == TripStatus.Pending)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => x.Id)
            .Take(15)
            .ToListAsync(cancellationToken);
        foreach (var id in tripIds)
        {
            await BroadcastAsync(id, cancellationToken);
        }
    }

    public async Task BroadcastDueScheduledAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leadEnd = now.Add(ScheduleBroadcastLead);
        var tripIds = await db.Trips
            .Where(x => x.Status == TripStatus.Pending
                && x.ScheduledAtUtc != null
                && x.ScheduledAtUtc <= leadEnd)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);
        foreach (var id in tripIds)
        {
            await BroadcastAsync(id, cancellationToken);
        }
    }

    public async Task ExpireTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var offers = await db.TripOffers
            .Where(x => x.TripId == tripId && x.Status == OfferStatus.Offered)
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

    public async Task ExpireStaleOnlineRidersAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - RiderPresence.OnlineTtl;
        var stale = await db.RiderProfiles
            .Where(x => x.IsOnline
                && (x.LastLocationAtUtc == null || x.LastLocationAtUtc < cutoff)
                && (x.OnlineAtUtc == null || x.OnlineAtUtc < cutoff))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return;
        }

        var ids = stale.Select(x => x.Id).ToList();
        foreach (var rider in stale)
        {
            rider.IsOnline = false;
            rider.UpdatedAtUtc = now;
        }

        var offers = await db.TripOffers
            .Where(x => ids.Contains(x.RiderId) && x.Status == OfferStatus.Offered)
            .ToListAsync(cancellationToken);
        foreach (var offer in offers)
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExpireStaleAsync(Guid? tripId, CancellationToken cancellationToken)
    {
        await ExpireStaleOnlineRidersAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var query = db.TripOffers.Where(x => x.Status == OfferStatus.Offered && x.ExpiresAtUtc < now);
        if (tripId is Guid id)
        {
            query = query.Where(x => x.TripId == id);
        }

        var stale = await query.ToListAsync(cancellationToken);
        foreach (var offer in stale)
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = now;
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static RiderOfferItem MapOffer(TripOffer offer, Trip trip) =>
        new(
            offer.Id,
            trip.Id,
            trip.Reference,
            trip.Status,
            trip.CustomerName,
            trip.CustomerPhone,
            TripAddress.Display(trip.PickupDetails, trip.Pickup),
            TripAddress.Display(trip.DropoffDetails, trip.Dropoff),
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Fare,
            trip.DistanceKm,
            Math.Max(1, trip.PassengerCount),
            offer.DistanceKm is decimal km ? (double)km : null,
            trip.VehicleType,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            RiderDisplayTime.ToApi(trip.RequestedAtUtc),
            RiderDisplayTime.ToApi(trip.ScheduledAtUtc),
            RiderDisplayTime.ToApi(offer.ExpiresAtUtc),
            offer.IsPreferred,
            offer.IsPreferred,
            trip.CustomerFare > 0 ? trip.CustomerFare : trip.Fare,
            trip.PromoDiscountAmount,
            trip.IsPromoSponsored,
            trip.DiscountPercent,
            trip.CustomerBoostAmount);

    public static RiderActiveTrip MapTrip(
        Trip trip,
        int previousBookingCount = 0,
        int completedBookingCount = 0,
        int cancelledBookingCount = 0,
        DateTime? lastCompletedAtUtc = null) =>
        new(
            trip.Id,
            trip.Reference,
            trip.Status,
            trip.CustomerName,
            trip.CustomerPhone,
            previousBookingCount,
            completedBookingCount,
            cancelledBookingCount,
            lastCompletedAtUtc is DateTime at ? RiderDisplayTime.ToApi(at) : null,
            TripAddress.Display(trip.PickupDetails, trip.Pickup),
            TripAddress.Display(trip.DropoffDetails, trip.Dropoff),
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Fare,
            trip.DistanceKm,
            Math.Max(1, trip.PassengerCount),
            trip.VehicleType,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            RiderDisplayTime.ToApi(trip.RequestedAtUtc),
            RiderDisplayTime.ToApi(trip.ScheduledAtUtc),
            trip.Status == TripStatus.Waiting,
            trip.Status == TripStatus.Ongoing,
            trip.Status is TripStatus.Pending or TripStatus.Waiting,
            trip.Status is TripStatus.Waiting or TripStatus.Ongoing,
            TripChatService.CanView(trip),
            TripChatService.CanChat(trip),
            trip.CustomerFare > 0 ? trip.CustomerFare : trip.Fare,
            trip.PromoDiscountAmount,
            trip.IsPromoSponsored,
            trip.DiscountPercent,
            trip.CustomerBoostAmount);

    public async Task<HashSet<Guid>> LiveHailedRiderIdsAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - HailTtl;
        var ids = await db.CustomerProfiles
            .Where(x => x.HailRiderId != null && x.HailAtUtc != null && x.HailAtUtc >= since)
            .Select(x => x.HailRiderId!.Value)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public static double ClampRadius(double km) =>
        Math.Clamp(km, MinBroadcastRadiusKm, MaxBroadcastRadiusKm);

    async Task<double> ResolveBroadcastRadiusKmAsync(Guid operatorId, CancellationToken cancellationToken)
    {
        var stored = await db.Operators.AsNoTracking()
            .Where(x => x.Id == operatorId)
            .Select(x => (double?)x.BroadcastRadiusKm)
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is null or <= 0)
        {
            return DefaultBroadcastRadiusKm;
        }

        return ClampRadius(stored.Value);
    }
}
