using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class OperatorPromoRules
{
    public static string CodeForPercent(int percent) => $"SAVE{percent}";

    public static string DisplayCode(int percent) => $"Save{percent}";

    public static int? ParseSavePercent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var code = raw.Trim().ToUpperInvariant();
        if (!code.StartsWith("SAVE", StringComparison.Ordinal) || code.Length < 5)
        {
            return null;
        }

        if (!int.TryParse(code[4..], out var percent) || percent is < 1 or > 100)
        {
            return null;
        }

        return percent;
    }

    public static (decimal CustomerFare, decimal DiscountAmount) SplitFare(decimal originalFare, int discountPercent)
    {
        var pct = Math.Clamp(discountPercent, 1, 100);
        var discount = CommissionCut.Round(originalFare * pct / 100m);
        if (discount > originalFare)
        {
            discount = originalFare;
        }

        return (CommissionCut.Round(originalFare - discount), discount);
    }
}

public class OperatorPromoService(AppDbContext db)
{
    public async Task<(OperatorPromo? Promo, string? Error)> ResolveForBookingAsync(
        Guid operatorId,
        Guid? customerId,
        string? promoCode,
        CancellationToken cancellationToken)
    {
        var percent = OperatorPromoRules.ParseSavePercent(promoCode);
        if (percent is null)
        {
            return (null, string.IsNullOrWhiteSpace(promoCode) ? null : "Promo code must look like Save10, Save50, or Save100.");
        }

        var code = OperatorPromoRules.CodeForPercent(percent.Value);
        var promo = await db.OperatorPromos
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.Code == code, cancellationToken);
        if (promo is null)
        {
            return (null, "That promo code is not available for this operator.");
        }

        var error = await ValidateUsableAsync(promo, customerId, cancellationToken);
        return error is null ? (promo, null) : (null, error);
    }

    public async Task<string?> ValidateUsableAsync(
        OperatorPromo promo,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        if (!promo.IsActive)
        {
            return "This promo is inactive.";
        }

        var now = DateTime.UtcNow;
        if (promo.StartsAtUtc is DateTime start && now < start)
        {
            return "This promo has not started yet.";
        }

        if (promo.EndsAtUtc is DateTime end && now > end)
        {
            return "This promo has ended.";
        }

        if (promo.MaxRedemptions is int max && promo.RedemptionCount >= max)
        {
            return "This promo has reached its redemption limit.";
        }

        if (customerId is Guid cid)
        {
            var used = await db.PromoRedemptions.AnyAsync(
                x => x.PromoId == promo.Id && x.CustomerId == cid,
                cancellationToken);
            if (used)
            {
                return "You already used this promo.";
            }
        }

        return null;
    }

    public async Task<bool> HasOfferablePromosAsync(
        Guid operatorId,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.OperatorPromos
            .Where(x => x.OperatorId == operatorId && x.IsActive)
            .Where(x => x.StartsAtUtc == null || x.StartsAtUtc <= now)
            .Where(x => x.EndsAtUtc == null || x.EndsAtUtc >= now)
            .Where(x => x.MaxRedemptions == null || x.RedemptionCount < x.MaxRedemptions)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return false;
        }

        if (customerId is not Guid cid)
        {
            return true;
        }

        var used = await db.PromoRedemptions
            .Where(x => x.CustomerId == cid && candidates.Contains(x.PromoId))
            .Select(x => x.PromoId)
            .ToListAsync(cancellationToken);
        return candidates.Any(id => !used.Contains(id));
    }

    public async Task RedeemOnCompleteAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (!trip.IsPromoSponsored || trip.PromoId is not Guid promoId || trip.CustomerId is not Guid customerId)
        {
            return;
        }

        if (await db.PromoRedemptions.AnyAsync(x => x.TripId == trip.Id, cancellationToken))
        {
            return;
        }

        if (await db.PromoRedemptions.AnyAsync(x => x.PromoId == promoId && x.CustomerId == customerId, cancellationToken))
        {
            return;
        }

        var promo = await db.OperatorPromos.FirstOrDefaultAsync(x => x.Id == promoId, cancellationToken);
        if (promo is null)
        {
            return;
        }

        db.PromoRedemptions.Add(new PromoRedemption
        {
            PromoId = promo.Id,
            CustomerId = customerId,
            TripId = trip.Id,
            RedeemedAtUtc = DateTime.UtcNow,
        });
        promo.RedemptionCount += 1;
        promo.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
