using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class PabiliSurchargeRules
{
    public static decimal ActiveAmount(IEnumerable<PabiliSurcharge>? surcharges, DateTime? utcNow = null)
    {
        if (surcharges is null)
        {
            return 0;
        }

        var nowUtc = utcNow ?? DateTime.UtcNow;
        var ph = PhilippineTime.ToPh(nowUtc);
        var phTime = TimeOnly.FromDateTime(ph);
        decimal total = 0;
        foreach (var row in surcharges.Where(x => x.IsActive))
        {
            if (!Matches(row, nowUtc, phTime))
            {
                continue;
            }

            total += row.Amount;
        }

        return CommissionCut.Round(total);
    }

    public static bool Matches(PabiliSurcharge row, DateTime utcNow, TimeOnly phTime)
    {
        if (row.Kind == SurchargeKind.DateRange)
        {
            if (row.RangeStartUtc is DateTime start && utcNow < start)
            {
                return false;
            }

            if (row.RangeEndUtc is DateTime end && utcNow > end)
            {
                return false;
            }

            return true;
        }

        if (row.Kind != SurchargeKind.TimeWindow || row.WindowStart is null || row.WindowEnd is null)
        {
            return false;
        }

        var startT = row.WindowStart.Value;
        var endT = row.WindowEnd.Value;
        if (startT == endT)
        {
            return true;
        }

        if (startT < endT)
        {
            return phTime >= startT && phTime < endT;
        }

        return phTime >= startT || phTime < endT;
    }
}
