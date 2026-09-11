using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class PabiliOrderBroadcastService(AppDbContext db, LiveNotify live)
{
    public static readonly TimeSpan OfferTtl = TimeSpan.FromMinutes(10);
    public const int MaxRiders = 20;

    public async Task BroadcastAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.PabiliOrders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null || order.Status != PabiliOrderStatus.Pending)
        {
            return;
        }

        await ExpireStaleAsync(order.Id, cancellationToken);

        var radiusKm = await ResolveBroadcastRadiusKmAsync(order.OperatorId, cancellationToken);
        var ranked = await RankEligibleAsync(
            order.OperatorId,
            order.PaymentMethod,
            order.PickupLat,
            order.PickupLng,
            radiusKm,
            cancellationToken);

        var existing = await db.PabiliOrderOffers
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var expires = now.Add(OfferTtl);
        var chosen = ranked.Take(MaxRiders).ToList();
        var notify = new HashSet<Guid>();

        foreach (var (rider, distance) in chosen)
        {
            var offer = existing.FirstOrDefault(x => x.RiderId == rider.Id);
            if (offer is null)
            {
                db.PabiliOrderOffers.Add(new PabiliOrderOffer
                {
                    OrderId = order.Id,
                    RiderId = rider.Id,
                    Status = OfferStatus.Offered,
                    DistanceKm = distance is double km ? Math.Round((decimal)km, 2) : null,
                    OfferedAtUtc = now,
                    ExpiresAtUtc = expires
                });
                notify.Add(rider.Id);
                continue;
            }

            if (offer.Status is OfferStatus.Declined or OfferStatus.Accepted)
            {
                continue;
            }

            if (offer.Status == OfferStatus.Offered && offer.ExpiresAtUtc > now.AddMinutes(1))
            {
                if (distance is double liveKm)
                {
                    offer.DistanceKm = Math.Round((decimal)liveKm, 2);
                }

                continue;
            }

            offer.Status = OfferStatus.Offered;
            offer.OfferedAtUtc = now;
            offer.ExpiresAtUtc = expires;
            offer.RespondedAtUtc = null;
            offer.DistanceKm = distance is double refreshKm ? Math.Round((decimal)refreshKm, 2) : offer.DistanceKm;
            offer.UpdatedAtUtc = now;
            notify.Add(rider.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var riderId in notify)
        {
            await live.RiderOfferAsync(riderId, order.Reference, cancellationToken);
        }
    }

    public async Task ExpireStaleAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var stale = await db.PabiliOrderOffers
            .Where(x => x.OrderId == orderId && x.Status == OfferStatus.Offered && x.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
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

    private async Task<double> ResolveBroadcastRadiusKmAsync(Guid operatorId, CancellationToken cancellationToken)
    {
        var stored = await db.Operators.AsNoTracking()
            .Where(x => x.Id == operatorId)
            .Select(x => (double?)x.BroadcastRadiusKm)
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is null or <= 0)
        {
            return TripBroadcastService.DefaultBroadcastRadiusKm;
        }

        return TripBroadcastService.ClampRadius(stored.Value);
    }

    private async Task<List<(RiderProfile Rider, double? Distance)>> RankEligibleAsync(
        Guid operatorId,
        PaymentMethod paymentMethod,
        double pickupLat,
        double pickupLng,
        double radiusKm,
        CancellationToken cancellationToken)
    {
        var tripBusy = await db.Trips
            .Where(x => x.OperatorId == operatorId
                && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .Select(x => x.RiderId)
            .ToListAsync(cancellationToken);
        var pabiliBusy = await db.PabiliOrders
            .Where(x => x.OperatorId == operatorId
                && x.RiderId != null
                && (x.Status == PabiliOrderStatus.Waiting
                    || x.Status == PabiliOrderStatus.PickedUp
                    || x.Status == PabiliOrderStatus.Delivering))
            .Select(x => x.RiderId!.Value)
            .ToListAsync(cancellationToken);
        var busy = tripBusy.Concat(pabiliBusy).ToHashSet();

        var riders = await db.RiderProfiles
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == operatorId
                && x.IsActive
                && x.AcceptsPabili
                && x.IsOnline
                && x.AppUser.IsActive)
            .ToListAsync(cancellationToken);

        var ranked = new List<(RiderProfile Rider, double? Distance)>();
        foreach (var rider in riders)
        {
            if (busy.Contains(rider.Id))
            {
                continue;
            }

            if (!rider.PaymentMethods.Any(x => x.Method == paymentMethod))
            {
                continue;
            }

            if (!TripBroadcastService.CanReceiveBookings(rider.Wallet?.Balance ?? 0))
            {
                continue;
            }

            var distance = Geo.DistanceKm(rider.LastLat, rider.LastLng, pickupLat, pickupLng);
            if (distance is double km && km > radiusKm)
            {
                continue;
            }

            ranked.Add((rider, distance));
        }

        return ranked.OrderBy(x => x.Distance ?? double.MaxValue).ToList();
    }
}
