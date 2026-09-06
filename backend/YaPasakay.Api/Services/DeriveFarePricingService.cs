using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public record LatLngPoint(double Lat, double Lng);

public static class GeoPolygon
{
    public static IReadOnlyList<LatLngPoint> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var points = new List<LatLngPoint>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("lat", out var latEl) || !el.TryGetProperty("lng", out var lngEl))
                {
                    continue;
                }

                if (latEl.TryGetDouble(out var lat) && lngEl.TryGetDouble(out var lng))
                {
                    points.Add(new LatLngPoint(lat, lng));
                }
            }

            return points;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<LatLngPoint> points) =>
        JsonSerializer.Serialize(points.Select(p => new { lat = p.Lat, lng = p.Lng }));

    public static bool Contains(IReadOnlyList<LatLngPoint> ring, double lat, double lng)
    {
        if (ring.Count < 3)
        {
            return false;
        }

        // Ray casting
        var inside = false;
        for (var i = 0; i < ring.Count; i++)
        {
            var j = (i + 1) % ring.Count;
            var yi = ring[i].Lat;
            var xi = ring[i].Lng;
            var yj = ring[j].Lat;
            var xj = ring[j].Lng;
            var intersect = ((yi > lat) != (yj > lat))
                && (lng < (xj - xi) * (lat - yi) / (yj - yi + double.Epsilon) + xi);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}

public class DeriveFarePricingService(AppDbContext db)
{
    public async Task<(DeriveFareZone? Zone, DeriveFareMatrix? Matrix, string? Error)> ResolveAsync(
        Guid operatorId,
        VehicleType vehicleType,
        double pickupLat,
        double pickupLng,
        decimal distanceKm,
        CancellationToken cancellationToken)
    {
        var zones = await db.DeriveFareZones
            .AsNoTracking()
            .Where(x => x.OperatorId == operatorId && x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        DeriveFareZone? matched = null;
        foreach (var zone in zones)
        {
            var ring = GeoPolygon.Parse(zone.PolygonJson);
            if (GeoPolygon.Contains(ring, pickupLat, pickupLng))
            {
                matched = zone;
                break;
            }
        }

        if (matched is null)
        {
            return (null, null, null);
        }

        if (distanceKm > matched.MaxDropoffKm)
        {
            return (matched, null,
                $"Drop-off is beyond the maximum {matched.MaxDropoffKm:0.##} km for {matched.Name}.");
        }

        var matrix = await db.DeriveFareMatrices
            .Include(x => x.PassengerTiers)
            .FirstOrDefaultAsync(
                x => x.DeriveFareZoneId == matched.Id
                    && x.VehicleType == vehicleType
                    && x.IsActive,
                cancellationToken);
        if (matrix is null)
        {
            return (matched, null, $"No derive fare rates for {vehicleType} in {matched.Name}.");
        }

        return (matched, matrix, null);
    }

    public static decimal ComputeWithSharedSurcharges(
        DeriveFareMatrix deriveMatrix,
        FareMatrix? municipalityMatrix,
        int passengerCount,
        decimal distanceKm)
    {
        var baseFare = FareQuote.ComputeForPassengers(deriveMatrix, passengerCount, distanceKm);
        var surcharge = FareSurchargeRules.ActiveAmount(municipalityMatrix?.Surcharges);
        return CommissionCut.Round(baseFare + surcharge);
    }

    public static decimal ComputeMunicipalityWithSurcharges(
        FareMatrix matrix,
        int passengerCount,
        decimal distanceKm)
    {
        var baseFare = FareQuote.ComputeForPassengers(matrix, passengerCount, distanceKm);
        var surcharge = FareSurchargeRules.ActiveAmount(matrix.Surcharges);
        return CommissionCut.Round(baseFare + surcharge);
    }
}
