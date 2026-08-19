using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using YaPasakay.Application.Common;

namespace YaPasakay.Api.Services;

public class GoogleDrivingDistance(IConfiguration config, ILogger<GoogleDrivingDistance> logger)
{
    private static readonly ConcurrentDictionary<string, (decimal Km, int EtaMinutes, DateTime AtUtc)> Cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(8);

    public async Task<(decimal Km, int EtaMinutes)> MeasureAsync(
        double pickupLat,
        double pickupLng,
        double dropoffLat,
        double dropoffLng,
        CancellationToken cancellationToken = default)
    {
        var straight = Geo.DistanceKm(pickupLat, pickupLng, dropoffLat, dropoffLng) ?? 4;
        var fallbackKm = Math.Round((decimal)Math.Max(straight, 1), 1, MidpointRounding.AwayFromZero);
        var fallbackEta = Math.Max(4, (int)Math.Round((double)fallbackKm / 0.35));

        var key = string.Create(CultureInfo.InvariantCulture, $"{pickupLat:F5},{pickupLng:F5}|{dropoffLat:F5},{dropoffLng:F5}");
        if (Cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.AtUtc < CacheTtl)
        {
            return (cached.Km, cached.EtaMinutes);
        }

        var apiKey = config["Maps:GoogleApiKey"] ?? config["Maps:BrowserApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Maps API key missing; using straight-line distance {Km} km", fallbackKm);
            return (fallbackKm, fallbackEta);
        }

        var origin = string.Create(CultureInfo.InvariantCulture, $"{pickupLat},{pickupLng}");
        var destination = string.Create(CultureInfo.InvariantCulture, $"{dropoffLat},{dropoffLng}");
        var url =
            "https://maps.googleapis.com/maps/api/directions/json"
            + $"?origin={Uri.EscapeDataString(origin)}"
            + $"&destination={Uri.EscapeDataString(destination)}"
            + "&mode=driving&units=metric"
            + $"&key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Directions HTTP {Status}; using straight-line {Km} km", (int)response.StatusCode, fallbackKm);
                return (fallbackKm, fallbackEta);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Directions status {Status}; using straight-line {Km} km", status, fallbackKm);
                return (fallbackKm, fallbackEta);
            }

            var meters = 0;
            var seconds = 0;
            if (root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
            {
                var route = routes[0];
                if (route.TryGetProperty("legs", out var legs) && legs.GetArrayLength() > 0)
                {
                    foreach (var leg in legs.EnumerateArray())
                    {
                        if (leg.TryGetProperty("distance", out var distance) && distance.TryGetProperty("value", out var metersEl))
                        {
                            meters += metersEl.GetInt32();
                        }

                        if (leg.TryGetProperty("duration", out var duration) && duration.TryGetProperty("value", out var secondsEl))
                        {
                            seconds += secondsEl.GetInt32();
                        }
                    }
                }
            }

            if (meters <= 0)
            {
                return (fallbackKm, fallbackEta);
            }

            var km = Math.Round((decimal)meters / 1000m, 1, MidpointRounding.AwayFromZero);
            if (km < 0.1m)
            {
                km = 0.1m;
            }

            var eta = seconds > 0
                ? Math.Max(1, (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero))
                : fallbackEta;

            Cache[key] = (km, eta, DateTime.UtcNow);
            return (km, eta);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Directions failed; using straight-line {Km} km", fallbackKm);
            return (fallbackKm, fallbackEta);
        }
    }
}
