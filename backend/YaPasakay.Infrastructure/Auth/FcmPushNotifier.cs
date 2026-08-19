using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Infrastructure.Auth;

public class FcmPushNotifier(
    AppDbContext db,
    IOptions<FcmOptions> options,
    ILogger<FcmPushNotifier> logger) : IPushNotifier
{
    public async Task SendToUserAsync(
        Guid appUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = await db.DeviceRegistrations.AsNoTracking()
            .Where(x => x.AppUserId == appUserId)
            .Select(x => x.Token)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            logger.LogInformation("Push skipped (no device) user={UserId} title={Title}", appUserId, title);
            return;
        }

        var key = options.Value.ServerKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogInformation(
                "Push (log-only) user={UserId} title={Title} body={Body} tokens={Count}",
                appUserId,
                title,
                body,
                tokens.Count);
            return;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        foreach (var token in tokens)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/fcm/send");
                request.Headers.Authorization = new AuthenticationHeaderValue("key", key);
                var payload = new Dictionary<string, object?>
                {
                    ["to"] = token,
                    ["priority"] = "high",
                    ["notification"] = new { title, body },
                    ["data"] = data ?? new Dictionary<string, string>(),
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("FCM {Status} for user {UserId}", (int)response.StatusCode, appUserId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FCM send failed for user {UserId}", appUserId);
            }
        }
    }
}
