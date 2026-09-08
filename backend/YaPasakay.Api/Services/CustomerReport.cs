using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class CustomerReport
{
    public const int ExportMaxRows = 10_000;

    public static async Task<CustomerReportResponse> BuildAsync(
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

        return new CustomerReportResponse(
            new PagedResult<CustomerReportItem>(pageItems, page, pageSize, total),
            summary);
    }

    public static async Task<IReadOnlyList<CustomerReportItem>> BuildRowsAsync(
        AppDbContext db,
        Guid operatorId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken cancellationToken)
    {
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

        var relatedIds = db.Trips
            .Where(x => x.OperatorId == operatorId && x.CustomerId != null)
            .Select(x => x.CustomerId!.Value)
            .Distinct();

        var customersQuery = db.CustomerProfiles
            .AsNoTracking()
            .Include(x => x.AppUser)
            .Where(x => relatedIds.Contains(x.Id));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            customersQuery = customersQuery.Where(x =>
                x.FirstName.Contains(term)
                || x.LastName.Contains(term)
                || x.AppUser.FullName.Contains(term)
                || x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term));
        }

        var customers = await customersQuery
            .OrderByDescending(x => x.AppUser.IsActive)
            .ThenBy(x => x.AppUser.FullName)
            .Take(Math.Clamp(take, 1, ExportMaxRows))
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return [];
        }

        var customerIds = customers.Select(x => x.Id).ToList();
        var trips = await db.Trips
            .AsNoTracking()
            .Where(x => x.OperatorId == operatorId
                && x.CustomerId != null
                && customerIds.Contains(x.CustomerId.Value)
                && (
                    (x.Status == TripStatus.Completed
                        && x.Fare > 0
                        && (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                        && (x.CompletedAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive)
                    || (x.Status == TripStatus.Cancelled
                        && (x.CancelledAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                        && (x.CancelledAtUtc ?? x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive)))
            .ToListAsync(cancellationToken);

        var completedByCustomer = trips
            .Where(x => x.Status == TripStatus.Completed)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var cancelByCustomer = trips
            .Where(x => x.Status == TripStatus.Cancelled)
            .GroupBy(x => x.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return customers.Select(customer =>
        {
            completedByCustomer.TryGetValue(customer.Id, out var customerTrips);
            customerTrips ??= [];
            cancelByCustomer.TryGetValue(customer.Id, out var cancelCount);
            decimal booking = 0;
            decimal spent = 0;
            foreach (var trip in customerTrips)
            {
                booking += trip.Fare;
                spent += trip.CustomerFare > 0 ? trip.CustomerFare : trip.Fare;
            }

            return new CustomerReportItem(
                customer.Id,
                customer.AppUser.FullName,
                customer.AppUser.PhoneNumber,
                DateTime.SpecifyKind(customer.CreatedAtUtc, DateTimeKind.Utc),
                customer.AppUser.IsActive,
                customerTrips.Count,
                cancelCount,
                CommissionCut.Round(booking),
                CommissionCut.Round(spent));
        }).ToList();
    }

    public static CustomerReportSummary Summarize(IReadOnlyList<CustomerReportItem> rows) =>
        new(
            rows.Count,
            rows.Sum(x => x.TotalRides),
            rows.Sum(x => x.TotalCancel),
            CommissionCut.Round(rows.Sum(x => x.BookingAmount)),
            CommissionCut.Round(rows.Sum(x => x.TotalSpent)));
}
