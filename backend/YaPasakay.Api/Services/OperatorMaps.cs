using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class OperatorMaps
{
    public static RiderListItem Rider(RiderProfile rider) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.VehicleType,
            rider.PlateNumber,
            rider.VehicleModel,
            rider.IsActive,
            rider.LicenseType,
            rider.LicenseNumber,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            UploadUrls.FromPath(rider.LicensePhotoPath),
            RiderPaymentSync.Map(rider.PaymentMethods));

    public static FleetRiderItem Fleet(RiderProfile rider, TripStatus? status, string? bookingReference) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.VehicleType,
            rider.PlateNumber,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            rider.LastLat!.Value,
            rider.LastLng!.Value,
            DateTime.SpecifyKind(rider.LastLocationAtUtc ?? rider.UpdatedAtUtc ?? rider.CreatedAtUtc, DateTimeKind.Utc),
            RiderPresence.IsLive(rider),
            status,
            bookingReference);

    public static RiderDetailResponse RiderDetail(RiderProfile rider) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.VehicleType,
            rider.PlateNumber,
            rider.VehicleModel,
            rider.IsActive,
            rider.LicenseType,
            rider.LicenseNumber,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            UploadUrls.FromPath(rider.LicensePhotoPath),
            rider.FullAddress,
            OperatorAddressSync.Map(rider),
            RiderPaymentSync.Map(rider.PaymentMethods));

    public static CustomerListItem Customer(CustomerProfile customer) =>
        new(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            CustomerDisplayName(customer),
            customer.AppUser.PhoneNumber,
            customer.CreatedAtUtc,
            customer.AppUser.IsActive,
            UploadUrls.FromPath(customer.PhotoPath),
            customer.DeleteStatus);

    public static CustomerDetailResponse CustomerDetail(CustomerProfile customer) =>
        new(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            CustomerDisplayName(customer),
            customer.AppUser.PhoneNumber,
            customer.CreatedAtUtc,
            customer.AppUser.IsActive,
            UploadUrls.FromPath(customer.PhotoPath),
            new CustomerDeleteRequestItem(
                customer.DeleteStatus,
                customer.DeleteRequestedAtUtc,
                customer.DeleteRequestReason,
                customer.DeleteResolvedAtUtc,
                customer.DeleteResolutionNote));

    public static string CustomerDisplayName(CustomerProfile customer)
    {
        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        return name.Length > 0 ? name : customer.AppUser.FullName;
    }

    public static RideDetailResponse RideDetail(Trip trip, FareMatrix? fareMatrix = null)
    {
        var rider = trip.Rider;
        DateTime? ended = trip.Status switch
        {
            TripStatus.Completed => trip.CompletedAtUtc,
            TripStatus.Cancelled => trip.CancelledAtUtc,
            TripStatus.Ongoing or TripStatus.Waiting => DateTime.UtcNow,
            _ => null
        };
        int? duration = ended is { } at
            ? Math.Max(1, (int)Math.Round((at - trip.RequestedAtUtc).TotalMinutes))
            : null;

        return new RideDetailResponse(
            trip.Id,
            trip.Reference,
            trip.Status,
            trip.CustomerName,
            trip.CustomerPhone,
            RideStop(trip.PickupDetails, trip.Pickup, trip.PickupBarangay),
            RideStop(trip.DropoffDetails, trip.Dropoff, trip.DropoffBarangay),
            TripAddress.Display(trip.PickupDetails, trip.Pickup),
            TripAddress.Display(trip.DropoffDetails, trip.Dropoff),
            trip.Notes,
            trip.Fare,
            trip.DistanceKm,
            Math.Max(1, trip.PassengerCount),
            duration,
            trip.VehicleType,
            trip.RequestedAtUtc,
            trip.ScheduledAtUtc is DateTime scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
            trip.CompletedAtUtc,
            trip.CancelledAtUtc,
            trip.CancelReason,
            trip.Rating,
            trip.RatingComment,
            trip.RatedAtUtc,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            trip.OperatorId,
            trip.Operator.CompanyName,
            trip.Operator.ContactPhone,
            trip.RiderId,
            rider?.AppUser.FullName ?? "Rider unavailable",
            rider?.AppUser.PhoneNumber ?? string.Empty,
            rider?.PlateNumber ?? string.Empty,
            rider?.VehicleModel,
            UploadUrls.FromPath(rider?.ProfilePhotoPath),
            (trip.ChatMessages ?? [])
                .OrderBy(x => x.SentAtUtc)
                .Select(TripChatService.Map)
                .ToList(),
            RideCommissionCalculator.ForTrip(trip, fareMatrix));
    }

    public static async Task<RideDetailResponse> RideDetailAsync(
        Trip trip,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var fare = await LoadFareMatrixAsync(db, trip, cancellationToken);
        return RideDetail(trip, fare);
    }

    public static async Task<FareMatrix?> LoadFareMatrixAsync(
        AppDbContext db,
        Trip trip,
        CancellationToken cancellationToken)
    {
        var municipalityId = await ResolvePickupMunicipalityIdAsync(db, trip, cancellationToken);
        if (municipalityId is null)
        {
            return null;
        }

        return await db.FareMatrices
            .FirstOrDefaultAsync(
                x => x.OperatorId == trip.OperatorId
                    && x.VehicleType == trip.VehicleType
                    && x.MunicipalityId == municipalityId
                    && x.IsActive,
                cancellationToken);
    }

    public static async Task<FareMatrix?> LoadFareMatrixAsync(
        AppDbContext db,
        Guid operatorId,
        VehicleType vehicleType,
        Guid municipalityId,
        CancellationToken cancellationToken) =>
        await db.FareMatrices
            .Include(x => x.PassengerTiers)
            .FirstOrDefaultAsync(
                x => x.OperatorId == operatorId
                    && x.VehicleType == vehicleType
                    && x.MunicipalityId == municipalityId
                    && x.IsActive,
                cancellationToken);

    public static async Task<Guid?> ResolvePickupMunicipalityIdAsync(
        AppDbContext db,
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.PickupBarangay is not null)
        {
            return trip.PickupBarangay.MunicipalityId;
        }

        if (trip.PickupBarangayId is Guid barangayId)
        {
            return await db.Barangays
                .Where(x => x.Id == barangayId)
                .Select(x => (Guid?)x.MunicipalityId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public static async Task<IReadOnlyList<IdName>> OperatorMunicipalitiesAsync(
        AppDbContext db,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        var covered = await db.OperatorBarangays
            .Where(x => x.OperatorId == operatorId)
            .Select(x => new { x.Barangay.MunicipalityId, x.Barangay.Municipality.Name })
            .Distinct()
            .ToListAsync(cancellationToken);
        var fromFares = await db.FareMatrices
            .Where(x => x.OperatorId == operatorId)
            .Select(x => new { MunicipalityId = x.MunicipalityId, x.Municipality.Name })
            .Distinct()
            .ToListAsync(cancellationToken);

        return covered
            .Concat(fromFares)
            .GroupBy(x => x.MunicipalityId)
            .Select(g => new IdName(g.Key, g.First().Name))
            .OrderBy(x => x.Name)
            .ToList();
    }

    public static RideStopItem RideStop(string details, string fullAddress, Barangay? barangay)
    {
        var display = TripAddress.Display(details, fullAddress);
        return new(
            display,
            string.Empty,
            string.Empty,
            string.Empty,
            display);
    }

    public static IQueryable<Trip> RideDetailQuery(AppDbContext db) =>
        db.Trips
            .Include(x => x.Operator)
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Include(x => x.PickupBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .Include(x => x.DropoffBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .Include(x => x.ChatMessages);

    public static (DateTime Start, DateTime EndExclusive, int Days) ResolveRideWindow(
        string range,
        DateOnly? from,
        DateOnly? to)
    {
        if (from.HasValue || to.HasValue)
        {
            var startDate = from ?? to!.Value;
            var endDate = to ?? from!.Value;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var days = Math.Clamp(endDate.DayNumber - startDate.DayNumber + 1, 1, 366);
            endDate = startDate.AddDays(days - 1);
            var start = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return (start, start.AddDays(days), days);
        }

        var presetDays = range.ToLowerInvariant() switch
        {
            "monthly" => 30,
            "yearly" => 365,
            _ => 7
        };
        var presetStart = DateTime.UtcNow.Date.AddDays(1 - presetDays);
        return (presetStart, DateTime.UtcNow.Date.AddDays(1), presetDays);
    }

    public static async Task<RiderRidesResponse> BuildRidesAsync(
        IQueryable<Trip> source,
        AppDbContext db,
        string range,
        DateOnly? from,
        DateOnly? to,
        string? q,
        TripStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var term = q?.Trim() ?? "";
        var query = source;
        int days;
        DateTime start;
        if (term.Length > 0)
        {
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.Pickup.Contains(term) ||
                x.PickupDetails.Contains(term) ||
                x.Dropoff.Contains(term) ||
                x.DropoffDetails.Contains(term));
            if (await query.AnyAsync(cancellationToken))
            {
                start = (await query.MinAsync(x => x.RequestedAtUtc, cancellationToken)).Date;
                var end = (await query.MaxAsync(x => x.RequestedAtUtc, cancellationToken)).Date;
                days = Math.Clamp((end - start).Days + 1, 1, 366);
            }
            else
            {
                start = DateTime.UtcNow.Date;
                days = 1;
            }
        }
        else
        {
            (start, var endExclusive, days) = ResolveRideWindow(range, from, to);
            query = query.Where(x => x.RequestedAtUtc >= start && x.RequestedAtUtc < endExclusive);
        }

        if (status is TripStatus tripStatus)
        {
            query = query.Where(x => x.Status == tripStatus);
        }

        var completedTrips = await query
            .Where(x => x.Status == TripStatus.Completed)
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .ToListAsync(cancellationToken);
        var fares = await LoadFareMatrixLookupAsync(db, completedTrips, cancellationToken);
        var (systemAmount, operatorAmount, driverAmount) = RideCommissionCalculator.Sum(completedTrips, fares);

        var summary = new RiderRideSummary(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Completed, cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Cancelled, cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Ongoing, cancellationToken),
            completedTrips.Sum(x => x.Fare),
            systemAmount,
            operatorAmount,
            driverAmount);

        var tripDays = await query
            .Where(x => x.Status == TripStatus.Completed && x.CompletedAtUtc != null)
            .GroupBy(x => x.CompletedAtUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var series = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = DateOnly.FromDateTime(start.AddDays(offset));
                var count = tripDays.FirstOrDefault(x => DateOnly.FromDateTime(x.Day) == day)?.Count ?? 0;
                return new RideSeriesPoint(day, count);
            })
            .ToList();

        var total = await query.CountAsync(cancellationToken);
        var tripRows = await query
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var pageFares = await LoadFareMatrixLookupAsync(db, tripRows, cancellationToken);
        var rides = tripRows
            .Select(x =>
            {
                FareMatrix? fare = null;
                if (x.PickupBarangay is not null)
                {
                    pageFares.TryGetValue((x.OperatorId, x.VehicleType, x.PickupBarangay.MunicipalityId), out fare);
                }

                return new RideListItem(
                    x.Id,
                    x.Reference,
                    x.RequestedAtUtc,
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
                    RideCommissionCalculator.ForTrip(x, fare));
            })
            .ToList();

        return new RiderRidesResponse(summary, series, new PagedResult<RideListItem>(rides, page, pageSize, total));
    }

    public static async Task<Dictionary<(Guid OperatorId, VehicleType VehicleType, Guid MunicipalityId), FareMatrix>> LoadFareMatrixLookupAsync(
        AppDbContext db,
        IReadOnlyList<Trip> trips,
        CancellationToken cancellationToken)
    {
        if (trips.Count == 0)
        {
            return [];
        }

        var operatorIds = trips.Select(x => x.OperatorId).Distinct().ToList();
        var fares = await db.FareMatrices
            .Where(x => operatorIds.Contains(x.OperatorId) && x.IsActive)
            .ToListAsync(cancellationToken);
        return fares
            .GroupBy(x => (x.OperatorId, x.VehicleType, x.MunicipalityId))
            .ToDictionary(g => g.Key, g => g.First());
    }

    public static FareRatesItem? FareRates(FareMatrix? fare, bool includeSamples)
    {
        if (fare is null)
        {
            return null;
        }

        var tiers = MapPassengerTiers(fare);
        var sampleTier = FareQuote.ResolveTier(fare, 1);
        var municipalityName = fare.Municipality?.Name ?? string.Empty;
        return new FareRatesItem(
            fare.VehicleType,
            fare.MunicipalityId,
            municipalityName,
            sampleTier?.BaseFare ?? fare.BaseFare,
            sampleTier?.PerKm ?? fare.PerKm,
            sampleTier?.MinimumFare ?? fare.MinimumFare,
            sampleTier?.IncludedKm ?? fare.IncludedKm,
            fare.OperatorCommissionPercent,
            fare.DriverCommissionPercent,
            fare.IsActive,
            tiers,
            (fare.Surcharges ?? [])
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name)
                .Select(x => new FareSurchargeItem(
                    x.Id,
                    x.Kind,
                    x.Name,
                    x.Amount,
                    x.WindowStart?.ToString("HH\\:mm"),
                    x.WindowEnd?.ToString("HH\\:mm"),
                    x.RangeStartUtc is DateTime start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null,
                    x.RangeEndUtc is DateTime end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
                    x.IsActive))
                .ToList(),
            includeSamples
                ? FareQuote.SamplesForPassengers(fare, sampleTier?.PassengerCount ?? 1)
                : []);
    }

    public static IReadOnlyList<FarePassengerTierItem> MapPassengerTiers(FareMatrix fare)
    {
        if (fare.PassengerTiers is { Count: > 0 })
        {
            return fare.PassengerTiers
                .OrderBy(x => x.PassengerCount)
                .Select(x => new FarePassengerTierItem(
                    x.PassengerCount,
                    x.BaseFare,
                    x.PerKm,
                    x.MinimumFare,
                    x.IncludedKm))
                .ToList();
        }

        return
        [
            new FarePassengerTierItem(
                1,
                fare.BaseFare,
                fare.PerKm,
                fare.MinimumFare,
                fare.IncludedKm)
        ];
    }

    public static SupportTicketItem Support(SupportTicket ticket)
    {
        var name = ticket.OpenedBy == SupportOpenedBy.Rider
            ? ticket.Rider?.AppUser.FullName ?? "Rider"
            : string.IsNullOrWhiteSpace(ticket.Customer?.FirstName)
                ? ticket.Trip?.CustomerName ?? "Customer"
                : $"{ticket.Customer.FirstName} {ticket.Customer.LastName}".Trim();
        var phone = ticket.OpenedBy == SupportOpenedBy.Rider
            ? ticket.Rider?.AppUser.PhoneNumber ?? ""
            : ticket.Customer?.AppUser.PhoneNumber ?? ticket.Trip?.CustomerPhone ?? "";
        var municipality = ticket.Trip?.PickupBarangay?.Municipality.Name;
        return new SupportTicketItem(
            ticket.Id,
            ticket.Kind,
            ticket.Status,
            ticket.OpenedBy,
            name,
            phone,
            ticket.Subject,
            ticket.Body,
            ticket.OperatorNotes,
            ticket.OperatorId,
            ticket.Operator.CompanyName,
            ticket.Operator.ContactPhone,
            string.IsNullOrWhiteSpace(municipality) ? ticket.Operator.AreaOfOperation : municipality,
            ticket.TripId,
            ticket.Trip?.Reference,
            DateTime.SpecifyKind(ticket.CreatedAtUtc, DateTimeKind.Utc),
            ticket.ClosedAtUtc is DateTime closed ? DateTime.SpecifyKind(closed, DateTimeKind.Utc) : null);
    }

    public static SupportTicketDetailResponse SupportDetail(SupportTicket ticket)
    {
        var item = Support(ticket);
        var trip = ticket.Trip;
        var booking = trip is not null ? RideDetail(trip) : null;
        var rider = trip?.Rider ?? ticket.Rider;

        return new SupportTicketDetailResponse(
            item,
            booking,
            MapPoint(ticket.SosLat, ticket.SosLng, "SOS pressed", ticket.SosAtUtc),
            MapPoint(rider?.LastLat, rider?.LastLng, rider?.AppUser.FullName ?? "Rider", rider?.LastLocationAtUtc),
            MapPoint(trip?.PickupLat, trip?.PickupLng, "Pickup", null),
            MapPoint(trip?.DropoffLat, trip?.DropoffLng, "Drop-off", null));
    }

    public static IQueryable<SupportTicket> SupportDetailQuery(AppDbContext db) =>
        db.SupportTickets
            .Include(x => x.Operator)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.Operator)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.Rider)
                    .ThenInclude(x => x!.AppUser)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.PickupBarangay)
                    .ThenInclude(x => x!.Municipality)
                        .ThenInclude(x => x!.Province)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.DropoffBarangay)
                    .ThenInclude(x => x!.Municipality)
                        .ThenInclude(x => x!.Province)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.ChatMessages)
            .Include(x => x.Rider)
                .ThenInclude(x => x!.AppUser)
            .Include(x => x.Customer)
                .ThenInclude(x => x!.AppUser);

    private static MapPointItem? MapPoint(double? lat, double? lng, string label, DateTime? atUtc) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180
            ? new MapPointItem(
                lat.Value,
                lng.Value,
                label,
                atUtc is DateTime stamp ? DateTime.SpecifyKind(stamp, DateTimeKind.Utc) : null)
            : null;
}
