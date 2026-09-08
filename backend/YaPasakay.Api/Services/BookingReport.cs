using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class BookingReport
{
    public const int ExportMaxRows = 10_000;

    public static async Task<BookingReportResponse> BuildAsync(
        AppDbContext db,
        Guid operatorId,
        string? bookingNo,
        string? rider,
        string? customer,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var rows = await BuildRowsAsync(
            db,
            operatorId,
            bookingNo,
            rider,
            customer,
            from,
            to,
            ExportMaxRows,
            cancellationToken);
        var summary = Summarize(rows);
        var total = rows.Count;
        var pageItems = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new BookingReportResponse(
            new PagedResult<BookingReportItem>(pageItems, page, pageSize, total),
            summary);
    }

    public static async Task<IReadOnlyList<BookingReportItem>> BuildRowsAsync(
        AppDbContext db,
        Guid operatorId,
        string? bookingNo,
        string? rider,
        string? customer,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken cancellationToken)
    {
        if (from is null && to is null
            && string.IsNullOrWhiteSpace(bookingNo)
            && string.IsNullOrWhiteSpace(rider)
            && string.IsNullOrWhiteSpace(customer))
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
            .Include(x => x.Customer)
            .ThenInclude(x => x!.AppUser)
            .Where(x => x.OperatorId == operatorId
                && x.Status == TripStatus.Completed
                && x.Fare > 0);

        if (!string.IsNullOrWhiteSpace(bookingNo))
        {
            var term = bookingNo.Trim();
            query = query.Where(x => x.Reference.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(rider))
        {
            var term = rider.Trim();
            query = query.Where(x =>
                x.Rider.AppUser.FullName.Contains(term)
                || x.Rider.PlateNumber.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            var term = customer.Trim();
            query = query.Where(x =>
                x.CustomerName.Contains(term)
                || (x.Customer != null && (
                    x.Customer.AppUser.FullName.Contains(term)
                    || x.Customer.FirstName.Contains(term)
                    || x.Customer.LastName.Contains(term))));
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

        var trips = await query
            .OrderByDescending(x => x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc)
            .Take(Math.Clamp(take, 1, ExportMaxRows))
            .ToListAsync(cancellationToken);

        var fares = await OperatorMaps.LoadFareMatrixLookupAsync(db, trips, cancellationToken);
        return trips.Select(trip =>
        {
            Domain.Entities.FareMatrix? fare = null;
            if (trip.PickupBarangay is not null)
            {
                fares.TryGetValue((trip.OperatorId, trip.VehicleType, trip.PickupBarangay.MunicipalityId), out fare);
            }

            var breakdown = RideCommissionCalculator.ForTrip(trip, fare);
            var when = trip.CompletedAtUtc ?? trip.ScheduledAtUtc ?? trip.RequestedAtUtc;
            var customerName = !string.IsNullOrWhiteSpace(trip.CustomerName)
                ? trip.CustomerName
                : trip.Customer?.AppUser.FullName ?? "—";

            return new BookingReportItem(
                trip.Id,
                DateTime.SpecifyKind(when, DateTimeKind.Utc),
                trip.Reference,
                trip.Rider.AppUser.FullName,
                trip.RiderId,
                customerName,
                trip.CustomerId,
                breakdown?.DriverAmount ?? 0,
                breakdown?.SystemAmount ?? 0,
                breakdown?.OperatorAmount ?? 0,
                CommissionCut.Round(trip.PromoDiscountAmount),
                CommissionCut.Round(trip.Fare),
                trip.Status);
        }).ToList();
    }

    public static BookingReportSummary Summarize(IReadOnlyList<BookingReportItem> rows) =>
        new(
            rows.Count,
            CommissionCut.Round(rows.Sum(x => x.RiderCommission)),
            CommissionCut.Round(rows.Sum(x => x.SystemCommission)),
            CommissionCut.Round(rows.Sum(x => x.OperatorCommission)),
            CommissionCut.Round(rows.Sum(x => x.Promo)),
            CommissionCut.Round(rows.Sum(x => x.Fare)));
}
