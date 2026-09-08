using YaPasakay.Application.Common;

namespace YaPasakay.Api.Services;

/// <summary>
/// Rider APK parses *Utc ISO then DateTime.toLocal(). Sending PH wall-clock without a Z
/// offset makes those numbers render as Manila time on any device timezone — no APK change.
/// </summary>
public static class RiderDisplayTime
{
    public static DateTime ToApi(DateTime utc) =>
        PhilippineTime.ToPh(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    public static DateTime? ToApi(DateTime? utc) =>
        utc is DateTime at ? ToApi(at) : null;

    /// <summary>Prefer scheduled pickup time when present (APK booking UI only shows requestedAt).</summary>
    public static DateTime PrimaryTripAt(DateTime requestedAtUtc, DateTime? scheduledAtUtc) =>
        ToApi(scheduledAtUtc ?? requestedAtUtc);
}
