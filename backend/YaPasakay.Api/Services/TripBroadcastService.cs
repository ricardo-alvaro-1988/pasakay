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
    public const double RadiusKm = 8;
    public const int MaxRiders = 20;
    public const string DirectHailNote = "Direct hail";
    public static readonly TimeSpan HailTtl = TimeSpan.FromMinutes(10);

    public static bool HailIsLive(DateTime? at) =>
        at is DateTime stamped && DateTime.UtcNow - stamped <= HailTtl;
    public static readonly TimeSpan LiveOfferTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ScheduledOfferTtl = TimeSpan.FromHours(2);

    public static bool CanReceiveBookings(decimal balance) => balance >= MinWalletToReceive;

    public static string WalletHighlight(decimal balance, bool canReceive) =>
        canReceive
            ? $"Keep at least ₱{MinWalletToReceive:0} in your wallet to receive bookings. Balance: ₱{balance:0.00}."
            : $"Wallet below ₱{MinWalletToReceive:0}. Cash in to receive bookings. Balance: ₱{balance:0.00}.";

    public async Task BroadcastAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null || trip.Status != TripStatus.Pending)
        {
            return;
        }

        await ExpireStaleAsync(trip.Id, cancellationToken);

        if (string.Equals(trip.Notes, DirectHailNote, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var outsideCoverage = await OperatorAreaSync.CoverageErrorAsync(
                db,
                trip.OperatorId,
                trip.PickupBarangayId,
                cancellationToken) is not null;

        var busy = await db.Trips
            .Where(x => x.OperatorId == trip.OperatorId
                && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .Select(x => x.RiderId)
            .ToListAsync(cancellationToken);
        var hailed = await LiveHailedRiderIdsAsync(cancellationToken);

        var riders = await db.RiderProfiles
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == trip.OperatorId
                && x.IsActive
                && x.IsOnline
                && x.AppUser.IsActive)
            .ToListAsync(cancellationToken);

        var existing = await db.TripOffers
            .Where(x => x.TripId == trip.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var expires = trip.ScheduledAtUtc is DateTime scheduled && scheduled > now.AddMinutes(15)
            ? now.Add(ScheduledOfferTtl)
            : now.Add(LiveOfferTtl);

        var ranked = new List<(RiderProfile Rider, double? Distance, bool Preferred)>();
        foreach (var rider in riders)
        {
            var preferred = rider.Id == trip.RiderId;
            if (outsideCoverage && !preferred)
            {
                continue;
            }

            if ((busy.Contains(rider.Id) || hailed.Contains(rider.Id)) && !preferred)
            {
                continue;
            }

            if (rider.VehicleType != trip.VehicleType)
            {
                continue;
            }

            if (!rider.PaymentMethods.Any(x => x.Method == trip.PaymentMethod))
            {
                continue;
            }

            var balance = rider.Wallet?.Balance ?? 0;
            if (!preferred && !CanReceiveBookings(balance))
            {
                continue;
            }

            var distance = Geo.DistanceKm(rider.LastLat, rider.LastLng, trip.PickupLat, trip.PickupLng);
            if (!preferred
                && trip.PickupLat is not null
                && trip.PickupLng is not null
                && distance is double km
                && km > RadiusKm)
            {
                continue;
            }

            ranked.Add((rider, distance, preferred));
        }

        var chosen = ranked
            .OrderByDescending(x => x.Preferred)
            .ThenBy(x => x.Distance ?? double.MaxValue)
            .Take(MaxRiders)
            .ToList();

        if (chosen.All(x => !x.Preferred))
        {
            var preferredRider = riders.FirstOrDefault(x => x.Id == trip.RiderId);
            if (preferredRider is not null && chosen.Count < MaxRiders)
            {
                var distance = Geo.DistanceKm(preferredRider.LastLat, preferredRider.LastLng, trip.PickupLat, trip.PickupLng);
                chosen.Insert(0, (preferredRider, distance, true));
            }
        }

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
            trip.Pickup,
            trip.Dropoff,
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Fare,
            trip.DistanceKm,
            offer.DistanceKm is decimal km ? (double)km : null,
            trip.VehicleType,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            DateTime.SpecifyKind(trip.RequestedAtUtc, DateTimeKind.Utc),
            trip.ScheduledAtUtc is DateTime scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
            DateTime.SpecifyKind(offer.ExpiresAtUtc, DateTimeKind.Utc),
            offer.IsPreferred,
            offer.IsPreferred);

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
            lastCompletedAtUtc is DateTime at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
            trip.Pickup,
            trip.Dropoff,
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Fare,
            trip.DistanceKm,
            trip.VehicleType,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            DateTime.SpecifyKind(trip.RequestedAtUtc, DateTimeKind.Utc),
            trip.ScheduledAtUtc is DateTime scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
            trip.Status == TripStatus.Waiting,
            trip.Status == TripStatus.Ongoing,
            trip.Status is TripStatus.Waiting or TripStatus.Ongoing,
            TripChatService.CanView(trip),
            TripChatService.CanChat(trip));

    public async Task<HashSet<Guid>> LiveHailedRiderIdsAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - HailTtl;
        var ids = await db.CustomerProfiles
            .Where(x => x.HailRiderId != null && x.HailAtUtc != null && x.HailAtUtc >= since)
            .Select(x => x.HailRiderId!.Value)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }
}
