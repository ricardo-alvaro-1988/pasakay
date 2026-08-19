using YaPasakay.Domain.Entities;

namespace YaPasakay.Api.Services;

public static class RiderPresence
{
    public static readonly TimeSpan OnlineTtl = TimeSpan.FromMinutes(5);

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
