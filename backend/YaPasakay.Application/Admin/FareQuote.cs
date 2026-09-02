using YaPasakay.Domain.Entities;

namespace YaPasakay.Application.Admin;

public static class FareQuote
{
    public static decimal Compute(decimal baseFare, decimal perKm, decimal minimumFare, decimal includedKm, decimal distanceKm)
    {
        var extra = Math.Max(0, distanceKm - includedKm);
        var raw = baseFare + extra * perKm;
        return Math.Round(Math.Max(minimumFare, raw), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeForPassengers(FareMatrix? matrix, int passengerCount, decimal distanceKm)
    {
        var count = Math.Max(1, passengerCount);
        if (matrix is null)
        {
            return Compute(50, 12, 50, 1, distanceKm);
        }

        var tier = ResolveTier(matrix, count);
        if (tier is not null)
        {
            return Compute(tier.BaseFare, tier.PerKm, tier.MinimumFare, tier.IncludedKm, distanceKm);
        }

        return Compute(matrix.BaseFare, matrix.PerKm, matrix.MinimumFare, matrix.IncludedKm, distanceKm);
    }

    public static FarePassengerTier? ResolveTier(FareMatrix matrix, int passengerCount)
    {
        var count = Math.Max(1, passengerCount);
        var tiers = matrix.PassengerTiers;
        if (tiers is null || tiers.Count == 0)
        {
            return null;
        }

        var exact = tiers.FirstOrDefault(x => x.PassengerCount == count);
        if (exact is not null)
        {
            return exact;
        }

        return tiers
            .Where(x => x.PassengerCount <= count)
            .OrderByDescending(x => x.PassengerCount)
            .FirstOrDefault()
            ?? tiers.OrderBy(x => x.PassengerCount).FirstOrDefault();
    }

    public static IReadOnlyList<FareSampleItem> Samples(decimal baseFare, decimal perKm, decimal minimumFare, decimal includedKm) =>
        Enumerable.Range(1, 10)
            .Select(km => new FareSampleItem(km, Compute(baseFare, perKm, minimumFare, includedKm, km)))
            .ToList();

    public static IReadOnlyList<FareSampleItem> SamplesForPassengers(FareMatrix matrix, int passengerCount) =>
        Enumerable.Range(1, 10)
            .Select(km => new FareSampleItem(km, ComputeForPassengers(matrix, passengerCount, km)))
            .ToList();
}
