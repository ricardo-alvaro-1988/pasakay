using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class CustomerDeskBuilder
{
    public static async Task<CustomerDeskResponse> BuildAsync(
        AppDbContext db,
        CustomerProfile customer,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var trips = await db.Trips
            .Include(x => x.Operator)
            .Include(x => x.Rider)
                .ThenInclude(x => x.AppUser)
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(40)
            .ToListAsync(cancellationToken);

        var items = trips.Select(MapTrip).ToList();
        var active = items.FirstOrDefault(x =>
            x.Status is TripStatus.Pending or TripStatus.Waiting or TripStatus.Ongoing
            && (x.ScheduledAtUtc is null || x.ScheduledAtUtc <= now));
        var scheduled = items
            .Where(x => x.ScheduledAtUtc is DateTime at && at > now && x.Status is TripStatus.Pending or TripStatus.Waiting)
            .OrderBy(x => x.ScheduledAtUtc)
            .ToList();
        var pendingRating = items.FirstOrDefault(x => x.CanRate);
        var places = await LoadPlacesAsync(db, cancellationToken);
        var map = places.FirstOrDefault(x => x.Lat != 0 && x.Lng != 0);
        var hail = await LoadHailAsync(db, customer, cancellationToken);

        return new CustomerDeskResponse(
            customer.Id,
            customer.DisplayName,
            customer.FirstName,
            customer.LastName,
            customer.AppUser.PhoneNumber,
            customer.AppUser.Email,
            customer.Gender,
            !string.IsNullOrWhiteSpace(customer.PinHash),
            customer.DeleteStatus,
            active,
            scheduled,
            items,
            places,
            map?.Lat,
            map?.Lng,
            hail,
            pendingRating,
            !PhoneNormalizer.TryNormalizePhMobile(customer.AppUser.PhoneNumber, out _, out _));
    }

    public static async Task<CustomerHailRider?> LoadHailAsync(
        AppDbContext db,
        CustomerProfile customer,
        CancellationToken cancellationToken)
    {
        if (customer.HailRiderId is not Guid riderId || !TripBroadcastService.HailIsLive(customer.HailAtUtc))
        {
            return null;
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.Id == riderId, cancellationToken);
        if (rider is null || !rider.IsActive || !rider.AppUser.IsActive || !rider.Operator.IsActive)
        {
            return null;
        }

        var busy = await db.Trips.AnyAsync(
            x => x.RiderId == rider.Id && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);

        return new CustomerHailRider(
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
            rider.PaymentMethods.Select(x => x.Method).OrderBy(x => x).ToList());
    }

    public static async Task<IReadOnlyList<CustomerPlaceItem>> LoadPlacesAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var areas = await db.OperatorBarangays
            .Where(x => x.Operator.IsActive)
            .Select(x => new
            {
                x.BarangayId,
                Barangay = x.Barangay.Name,
                Municipality = x.Barangay.Municipality.Name,
                Province = x.Barangay.Municipality.Province.Name
            })
            .Distinct()
            .OrderBy(x => x.Province)
            .ThenBy(x => x.Municipality)
            .ThenBy(x => x.Barangay)
            .ToListAsync(cancellationToken);

        var tripCoords = await db.Trips
            .Where(x => x.PickupBarangayId != null && x.PickupLat != null && x.PickupLng != null)
            .GroupBy(x => x.PickupBarangayId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Lat = g.Average(x => x.PickupLat!.Value),
                Lng = g.Average(x => x.PickupLng!.Value)
            })
            .ToListAsync(cancellationToken);
        var coords = tripCoords.ToDictionary(x => x.Id);

        var riders = await db.RiderProfiles
            .Where(x => x.IsActive && x.LastLat != null && x.LastLng != null)
            .Select(x => new { x.LastLat, x.LastLng })
            .ToListAsync(cancellationToken);
        var riderLat = riders.Count == 0 ? 0d : riders.Average(x => x.LastLat!.Value);
        var riderLng = riders.Count == 0 ? 0d : riders.Average(x => x.LastLng!.Value);

        return areas.Select(area =>
        {
            coords.TryGetValue(area.BarangayId, out var pin);
            var lat = pin?.Lat ?? riderLat;
            var lng = pin?.Lng ?? riderLng;
            var details = $"{area.Barangay}, {area.Municipality}, {area.Province}";
            return new CustomerPlaceItem(
                area.BarangayId,
                area.Barangay,
                details,
                area.Barangay,
                area.Municipality,
                lat,
                lng);
        }).ToList();
    }

    public static CustomerTripItem MapTrip(Trip trip)
    {
        var rider = trip.Rider;
        var showRider = trip.Status is TripStatus.Waiting or TripStatus.Ongoing or TripStatus.Completed
            && rider is not null;
        var liveRider = showRider && trip.Status is TripStatus.Waiting or TripStatus.Ongoing;
        return new(
            trip.Id,
            trip.Reference,
            trip.Status,
            trip.Pickup,
            trip.Dropoff,
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Fare,
            trip.DistanceKm,
            trip.VehicleType,
            trip.PaymentMethod == 0 ? PaymentMethod.Cash : trip.PaymentMethod,
            trip.PaymentMethodOther,
            trip.Operator.CompanyName,
            showRider ? rider!.AppUser.FullName : null,
            showRider ? rider!.AppUser.PhoneNumber : null,
            showRider ? rider!.PlateNumber : null,
            showRider ? rider!.VehicleModel : null,
            showRider ? UploadUrls.FromPath(rider!.ProfilePhotoPath) : null,
            liveRider ? rider!.LastLat : null,
            liveRider ? rider!.LastLng : null,
            DateTime.SpecifyKind(trip.RequestedAtUtc, DateTimeKind.Utc),
            trip.ScheduledAtUtc is DateTime scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
            trip.Status is TripStatus.Pending or TripStatus.Waiting,
            trip.Status is TripStatus.Waiting or TripStatus.Ongoing,
            string.Equals(trip.Notes, TripBroadcastService.DirectHailNote, StringComparison.OrdinalIgnoreCase),
            trip.Rating,
            trip.RatingComment,
            trip.Status == TripStatus.Completed && trip.Rating is null,
            TripChatService.CanView(trip),
            TripChatService.CanChat(trip));
    }
}
