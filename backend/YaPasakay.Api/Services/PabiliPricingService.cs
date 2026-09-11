using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class PabiliPricingService(AppDbContext db, GoogleDrivingDistance driving)
{
    public async Task<(decimal DeliveryFee, decimal SurchargeTotal, decimal DistanceKm, string? Error)> QuoteDeliveryAsync(
        Guid operatorId,
        double pickupLat,
        double pickupLng,
        double dropoffLat,
        double dropoffLng,
        CancellationToken cancellationToken)
    {
        var matrix = await db.PabiliMatrices
            .Include(x => x.Surcharges)
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.IsActive, cancellationToken);
        if (matrix is null)
        {
            return (0, 0, 0, "Pabili delivery pricing is not configured for this area.");
        }

        var (distanceKm, _) = await driving.MeasureAsync(pickupLat, pickupLng, dropoffLat, dropoffLng, cancellationToken);
        var beyond = Math.Max(0m, distanceKm - matrix.KmScope);
        var succeeding = CommissionCut.Round(beyond * matrix.SucceedingKm);
        var surcharge = PabiliSurchargeRules.ActiveAmount(matrix.Surcharges);
        var delivery = CommissionCut.Round(matrix.BaseFareAmount + succeeding + surcharge);
        return (delivery, surcharge, distanceKm, null);
    }

    public static decimal CustomerTotal(decimal goods, decimal delivery, decimal adjustment) =>
        CommissionCut.Round(Math.Max(0, goods + delivery + adjustment));

    public static bool IsMerchantOpen(Merchant merchant, DateTime? utcNow = null)
    {
        var ph = PhilippineTime.ToPh(utcNow ?? DateTime.UtcNow);
        var day = (int)ph.DayOfWeek;
        var time = ph.TimeOfDay;
        var hours = merchant.OperatingHours.FirstOrDefault(x => x.DayOfWeek == day);
        if (hours is null)
        {
            return true;
        }

        if (hours.IsClosed)
        {
            return false;
        }

        if (hours.OpenTime is null || hours.CloseTime is null)
        {
            return true;
        }

        var open = hours.OpenTime.Value;
        var close = hours.CloseTime.Value;
        if (open == close)
        {
            return true;
        }

        if (open < close)
        {
            return time >= open && time < close;
        }

        return time >= open || time < close;
    }

    public static bool IsProductAvailableNow(MerchantProduct product, DateTime? utcNow = null)
    {
        if (!product.AvailableOnStorefront)
        {
            return false;
        }

        if (product.AvailableFromTime is null && product.AvailableToTime is null)
        {
            return true;
        }

        var ph = PhilippineTime.ToPh(utcNow ?? DateTime.UtcNow).TimeOfDay;
        var from = product.AvailableFromTime ?? TimeSpan.Zero;
        var to = product.AvailableToTime ?? TimeSpan.FromDays(1);
        if (from == to)
        {
            return true;
        }

        if (from < to)
        {
            return ph >= from && ph < to;
        }

        return ph >= from || ph < to;
    }
}
