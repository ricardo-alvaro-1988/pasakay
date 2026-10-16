using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class CashInExport
{
    public const int ExportMaxRows = 10_000;

    public static async Task<IReadOnlyList<WalletHistoryItem>> BuildRowsAsync(
        AppDbContext db,
        Guid operatorId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        WalletTransactionStatus? status,
        CancellationToken cancellationToken)
    {
        // Default window: today (PH ≈ UTC+8), matching other operator reports.
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

        var query = db.RiderWalletTransactions
            .AsNoTracking()
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Where(x => x.Rider.OperatorId == operatorId
                && x.Kind == WalletTransactionKind.CashIn
                && x.CreatedAtUtc >= start
                && x.CreatedAtUtc < endExclusive);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Rider.AppUser.FullName.Contains(term)
                || x.Rider.AppUser.PhoneNumber.Contains(term)
                || x.Rider.PlateNumber.Contains(term)
                || (x.Note != null && x.Note.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(ExportMaxRows)
            .ToListAsync(cancellationToken);

        return rows.Select(tx => new WalletHistoryItem(
            tx.Id,
            tx.RiderId,
            tx.Rider.AppUser.FullName,
            tx.Rider.AppUser.PhoneNumber,
            tx.Rider.PlateNumber,
            tx.Kind,
            tx.Status,
            tx.PaymentMethod,
            tx.Amount,
            tx.BalanceAfter,
            tx.TripId,
            null,
            null,
            null,
            tx.Note,
            tx.RejectionReason,
            tx.CreatedAtUtc,
            tx.ResolvedAtUtc)).ToList();
    }
}
