using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class FareQuote
{
    public static decimal Compute(decimal baseFare, decimal perKm, decimal minimumFare, decimal includedKm, decimal distanceKm)
    {
        var extra = Math.Max(0, distanceKm - includedKm);
        var raw = baseFare + extra * perKm;
        return Math.Round(Math.Max(minimumFare, raw), 2, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<FareSampleItem> Samples(decimal baseFare, decimal perKm, decimal minimumFare, decimal includedKm) =>
        Enumerable.Range(1, 10)
            .Select(km => new FareSampleItem(km, Compute(baseFare, perKm, minimumFare, includedKm, km)))
            .ToList();
}
