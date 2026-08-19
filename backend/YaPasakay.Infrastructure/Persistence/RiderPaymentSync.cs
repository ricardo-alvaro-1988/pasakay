using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Infrastructure.Persistence;

public static class RiderPaymentSync
{
    public static IReadOnlyList<PaymentMethod> Map(IEnumerable<RiderPaymentMethod> rows) =>
        rows.Select(x => x.Method).OrderBy(x => x).ToList();

    public static async Task<(bool Ok, string? Error)> SyncAsync(
        AppDbContext db,
        RiderProfile rider,
        IReadOnlyList<PaymentMethod> methods,
        CancellationToken cancellationToken)
    {
        var distinct = methods.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return (false, "Select at least one payment method this rider accepts.");
        }

        var existing = await db.RiderPaymentMethods
            .Where(x => x.RiderId == rider.Id)
            .ToListAsync(cancellationToken);

        foreach (var row in existing.Where(x => !distinct.Contains(x.Method)))
        {
            db.RiderPaymentMethods.Remove(row);
        }

        var current = existing.Select(x => x.Method).ToHashSet();
        foreach (var method in distinct.Where(x => !current.Contains(x)))
        {
            db.RiderPaymentMethods.Add(new RiderPaymentMethod
            {
                RiderId = rider.Id,
                Method = method
            });
        }

        return (true, null);
    }

    public static async Task<bool> AcceptsAsync(
        AppDbContext db,
        Guid riderId,
        PaymentMethod method,
        CancellationToken cancellationToken) =>
        await db.RiderPaymentMethods.AnyAsync(
            x => x.RiderId == riderId && x.Method == method,
            cancellationToken);

    public static string? ValidateTripPayment(PaymentMethod method, string? other)
    {
        if (method != PaymentMethod.Other)
        {
            return null;
        }

        var label = (other ?? string.Empty).Trim();
        return label.Length == 0 ? "Describe the other payment method." : null;
    }
}
