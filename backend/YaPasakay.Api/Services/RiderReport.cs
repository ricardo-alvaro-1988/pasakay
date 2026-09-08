using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class RiderReport
{
    public const int ExportMaxRows = 10_000;

    public static async Task<RiderReportResponse> BuildAsync(
        AppDbContext db,
        Guid operatorId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var rows = await BuildRowsAsync(db, operatorId, q, from, to, ExportMaxRows, cancellationToken);
        var summary = Summarize(rows);
        var total = rows.Count;
        var pageItems = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new RiderReportResponse(
            new PagedResult<RiderReportItem>(pageItems, page, pageSize, total),
            summary);
    }

    public static async Task<IReadOnlyList<RiderReportItem>> BuildRowsAsync(
        AppDbContext db,
        Guid operatorId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken cancellationToken)
    {
        // Default window: today (PH ≈ UTC+8), matching commission report.
        if (from is null && to is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            from = today;
            to = today;
        }

        var startDay = from ?? to!.Value;
        var endDay = to ?? from!.Value;
        if (endDay < startDay)
        {
            (startDay, endDay) = (endDay, startDay);
        }

        var start = DateTime.SpecifyKind(startDay.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);
        var endExclusive = DateTime.SpecifyKind(endDay.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);

        var ridersQuery = db.RiderProfiles
            .AsNoTracking()
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == operatorId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            ridersQuery = ridersQuery.Where(x =>
                x.AppUser.FullName.Contains(term)
                || x.AppUser.PhoneNumber.Contains(term)
                || x.PlateNumber.Contains(term)
                || x.VehicleFranchiseNumber.Contains(term));
        }

        var riders = await ridersQuery
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.AppUser.FullName)
            .Take(Math.Clamp(take, 1, ExportMaxRows))
            .ToListAsync(cancellationToken);

        if (riders.Count == 0)
        {
            return [];
        }

        var riderIds = riders.Select(x => x.Id).ToList();
        var trips = await db.Trips
            .AsNoTracking()
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .Where(x => x.OperatorId == operatorId
                && riderIds.Contains(x.RiderId)
                && x.Status == TripStatus.Completed
                && x.Fare > 0
                && (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                && (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive)
            .ToListAsync(cancellationToken);

        var fares = await OperatorMaps.LoadFareMatrixLookupAsync(db, trips, cancellationToken);
        var byRider = trips.GroupBy(x => x.RiderId).ToDictionary(g => g.Key, g => g.ToList());

        return riders.Select(rider =>
        {
            byRider.TryGetValue(rider.Id, out var riderTrips);
            riderTrips ??= [];
            decimal income = 0;
            decimal booking = 0;
            foreach (var trip in riderTrips)
            {
                booking += trip.Fare;
                Domain.Entities.FareMatrix? fare = null;
                if (trip.PickupBarangay is not null)
                {
                    fares.TryGetValue((trip.OperatorId, trip.VehicleType, trip.PickupBarangay.MunicipalityId), out fare);
                }

                var breakdown = RideCommissionCalculator.ForTrip(trip, fare);
                if (breakdown is not null)
                {
                    income += breakdown.DriverAmount;
                }
            }

            return new RiderReportItem(
                rider.Id,
                rider.AppUser.FullName,
                rider.PlateNumber,
                rider.VehicleFranchiseNumber,
                rider.AppUser.PhoneNumber,
                DateTime.SpecifyKind(rider.CreatedAtUtc, DateTimeKind.Utc),
                rider.IsActive,
                riderTrips.Count,
                CommissionCut.Round(income),
                CommissionCut.Round(booking));
        }).ToList();
    }

    public static RiderReportSummary Summarize(IReadOnlyList<RiderReportItem> rows) =>
        new(
            rows.Count,
            rows.Sum(x => x.TotalRides),
            CommissionCut.Round(rows.Sum(x => x.RiderIncome)),
            CommissionCut.Round(rows.Sum(x => x.BookingAmount)));
}
