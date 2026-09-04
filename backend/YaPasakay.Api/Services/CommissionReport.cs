using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class CommissionReport
{
    public static async Task<CommissionReportResponse> BuildAsync(
        AppDbContext db,
        Guid? operatorId,
        string? bookingNo,
        string? rider,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // Default window: today (local PH ≈ UTC+8; store compares on UTC date of trip activity).
        if (from is null && to is null && string.IsNullOrWhiteSpace(bookingNo) && string.IsNullOrWhiteSpace(rider))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            from = today;
            to = today;
        }

        var query = db.Trips
            .AsNoTracking()
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Include(x => x.Operator)
            .Include(x => x.PickupBarangay)
            .Where(x => x.Status == TripStatus.Completed && x.Fare > 0);

        if (operatorId is Guid opId)
        {
            query = query.Where(x => x.OperatorId == opId);
        }

        if (!string.IsNullOrWhiteSpace(bookingNo))
        {
            var term = bookingNo.Trim();
            query = query.Where(x => x.Reference.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(rider))
        {
            var term = rider.Trim();
            query = query.Where(x =>
                x.Rider.AppUser.FullName.Contains(term) ||
                x.Rider.PlateNumber.Contains(term));
        }

        if (from is not null || to is not null)
        {
            var startDay = from ?? to!.Value;
            var endDay = to ?? from!.Value;
            if (endDay < startDay)
            {
                (startDay, endDay) = (endDay, startDay);
            }

            var start = DateTime.SpecifyKind(startDay.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);
            var endExclusive = DateTime.SpecifyKind(endDay.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);
            query = query.Where(x =>
                (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                && (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive);
        }

        var total = await query.CountAsync(cancellationToken);
        var trips = await query
            .OrderByDescending(x => x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var fares = await OperatorMaps.LoadFareMatrixLookupAsync(db, trips, cancellationToken);
        var items = trips.Select(trip =>
        {
            var municipalityId = trip.PickupBarangay?.MunicipalityId;
            Domain.Entities.FareMatrix? fare = null;
            if (municipalityId is Guid mid)
            {
                fares.TryGetValue((trip.OperatorId, trip.VehicleType, mid), out fare);
            }

            var breakdown = RideCommissionCalculator.ForTrip(trip, fare);
            var when = trip.CompletedAtUtc ?? trip.ScheduledAtUtc ?? trip.RequestedAtUtc;
            return new CommissionReportItem(
                trip.Id,
                trip.Reference,
                trip.Rider.AppUser.FullName,
                trip.RiderId,
                trip.Operator.CompanyName,
                trip.OperatorId,
                breakdown?.DriverAmount ?? 0,
                breakdown?.OperatorAmount ?? 0,
                breakdown?.SystemAmount ?? 0,
                trip.Fare,
                DateTime.SpecifyKind(when, DateTimeKind.Utc),
                trip.Status);
        }).ToList();

        // Summary over the filtered set (not only the page) — cap for safety.
        var summaryTrips = await query
            .OrderByDescending(x => x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc)
            .Take(2000)
            .ToListAsync(cancellationToken);
        var summaryFares = await OperatorMaps.LoadFareMatrixLookupAsync(db, summaryTrips, cancellationToken);
        var (system, opAmount, driver) = RideCommissionCalculator.Sum(summaryTrips, summaryFares);
        var gross = CommissionCut.Round(summaryTrips.Sum(x => x.Fare));

        return new CommissionReportResponse(
            new PagedResult<CommissionReportItem>(items, page, pageSize, total),
            new CommissionReportSummary(gross, driver, opAmount, system, summaryTrips.Count));
    }
}
