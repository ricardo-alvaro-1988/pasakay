using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using YaPasakay.Application.Common;

namespace YaPasakay.Api.Services;

public class GoogleDrivingDistance(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<GoogleDrivingDistance> logger)
{
    private static readonly ConcurrentDictionary<string, (decimal Km, int EtaMinutes, DateTime AtUtc)> Cache = new();
    private static readonly ConcurrentDictionary<string, Task<(decimal Km, int EtaMinutes)>> Inflight = new();
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

        while (true)
        {
            if (Cache.TryGetValue(key, out cached) && DateTime.UtcNow - cached.AtUtc < CacheTtl)
            {
                return (cached.Km, cached.EtaMinutes);
            }

            if (Inflight.TryGetValue(key, out var existing))
            {
                return await existing.WaitAsync(cancellationToken);
            }

            var run = MeasureUncachedAsync(key, pickupLat, pickupLng, dropoffLat, dropoffLng, fallbackKm, fallbackEta, cancellationToken);
            if (!Inflight.TryAdd(key, run))
            {
                continue;
            }

            try
            {
                return await run;
            }
            finally
            {
                Inflight.TryRemove(key, out _);
            }
        }
    }

    private async Task<(decimal Km, int EtaMinutes)> MeasureUncachedAsync(
        string key,
        double pickupLat,
        double pickupLng,
        double dropoffLat,
        double dropoffLng,
        decimal fallbackKm,
        int fallbackEta,
        CancellationToken cancellationToken)
    {
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

        var payload = JsonSerializer.Serialize(new
        {
            origin = new { location = new { latLng = new { latitude = pickupLat, longitude = pickupLng } } },
            destination = new { location = new { latLng = new { latitude = dropoffLat, longitude = dropoffLng } } },
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_UNAWARE",
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://routes.googleapis.com/directions/v2:computeRoutes");
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey.Trim());
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", "routes.duration,routes.distanceMeters");
            AddMapsReferer(request);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var response = await client.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Routes API HTTP {Status}: {Body}; using straight-line {Km} km", (int)response.StatusCode, Truncate(body), fallbackKm);
                return (fallbackKm, fallbackEta);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                return (fallbackKm, fallbackEta);
            }

            var route = routes[0];
            var meters = route.TryGetProperty("distanceMeters", out var metersEl) ? metersEl.GetInt32() : 0;
            var seconds = ParseDurationSeconds(route);

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
            logger.LogWarning(ex, "Routes API failed; using straight-line {Km} km", fallbackKm);
            return (fallbackKm, fallbackEta);
        }
    }

    /// <summary>
    /// Best-effort reverse geocode for Plus Code / pin pickups so municipality matching can run.
    /// Uses Places API (New) nearby search because Geocoding REST rejects HTTP-referrer keys.
    /// </summary>
    public async Task<string?> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var apiKey = config["Maps:GoogleApiKey"] ?? config["Maps:BrowserApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new
        {
            maxResultCount = 5,
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude = lat, longitude = lng },
                    radius = 80.0,
                },
            },
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchNearby");
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey.Trim());
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", "places.formattedAddress,places.displayName,places.types");
            AddMapsReferer(request);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("places", out var places) || places.GetArrayLength() == 0)
            {
                return null;
            }

            string? best = null;
            var bestScore = -1;
            foreach (var item in places.EnumerateArray())
            {
                var addr = item.TryGetProperty("formattedAddress", out var addrEl)
                    ? addrEl.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(addr)
                    && item.TryGetProperty("displayName", out var nameEl)
                    && nameEl.TryGetProperty("text", out var textEl))
                {
                    addr = textEl.GetString();
                }

                if (string.IsNullOrWhiteSpace(addr))
                {
                    continue;
                }

                var score = addr.Length;
                if (item.TryGetProperty("types", out var types))
                {
                    foreach (var t in types.EnumerateArray())
                    {
                        var type = t.GetString() ?? "";
                        if (type is "street_address" or "premise" or "route") score += 80;
                        if (type is "locality" or "administrative_area_level_2") score += 50;
                        if (type is "neighborhood" or "sublocality" or "sublocality_level_1") score += 40;
                        if (type == "plus_code") score -= addr.Contains(',') ? 10 : 120;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = addr;
                }
            }

            return best;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Places reverse lookup failed for {Lat},{Lng}", lat, lng);
            return null;
        }
    }

    private void AddMapsReferer(HttpRequestMessage request)
    {
        var referer = (config["Maps:Referer"] ?? config["PublicOrigin"] ?? string.Empty).Trim().TrimEnd('/');
        if (referer.Length == 0)
        {
            return;
        }

        if (!referer.EndsWith('/'))
        {
            referer += "/";
        }

        request.Headers.Referrer = new Uri(referer);
    }

    private static int ParseDurationSeconds(JsonElement route)
    {
        if (!route.TryGetProperty("duration", out var durationEl))
        {
            return 0;
        }

        if (durationEl.ValueKind == JsonValueKind.String)
        {
            var text = durationEl.GetString() ?? "";
            if (text.EndsWith("s", StringComparison.Ordinal) &&
                double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return (int)Math.Round(seconds);
            }
        }

        if (durationEl.ValueKind == JsonValueKind.Number)
        {
            return durationEl.GetInt32();
        }

        return 0;
    }

    private static string Truncate(string body)
        => body.Length <= 240 ? body : body[..240];
}
