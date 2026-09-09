using YaPasakay.Domain.Entities;

namespace YaPasakay.Api.Services;

public static class RiderPresence
{
    /// <summary>How fresh location/online must be to count as "live" on the fleet map.</summary>
    public static readonly TimeSpan OnlineTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Only force IsOnline=false after this long with no heartbeat.
    /// Idle phones often pause GPS; do not drop Online just because the rider is stationary.
    /// </summary>
    public static readonly TimeSpan AbandonedOnlineTtl = TimeSpan.FromHours(2);

    public static bool IsLive(RiderProfile rider, DateTime? now = null) =>
        IsLive(rider.IsOnline, rider.LastLocationAtUtc, rider.OnlineAtUtc, now);

    public static bool IsLive(bool isOnline, DateTime? lastLocationAtUtc, DateTime? onlineAtUtc, DateTime? now = null)
    {
        if (!isOnline)
        {
            return false;
        }

        var clock = now ?? DateTime.UtcNow;
        var heartbeat = lastLocationAtUtc is DateTime location && onlineAtUtc is DateTime online
            ? (location > online ? location : online)
            : lastLocationAtUtc ?? onlineAtUtc;
        return heartbeat is DateTime at && clock - at <= OnlineTtl;
    }
}
