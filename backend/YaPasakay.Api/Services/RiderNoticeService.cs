using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Common;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class RiderNoticeService(AppDbContext db, LiveNotify live)
{
    public static bool TryParseMinute(string? value, out int minuteOfDay)
    {
        minuteOfDay = 0;
        var raw = (value ?? string.Empty).Trim();
        var parts = raw.Split(':');
        if (parts.Length < 2
            || !int.TryParse(parts[0], out var hour)
            || !int.TryParse(parts[1], out var minute))
        {
            return false;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        minuteOfDay = (hour * 60) + minute;
        return true;
    }

    public static string FormatMinute(int minuteOfDay)
    {
        var hour = Math.Clamp(minuteOfDay, 0, (23 * 60) + 59) / 60;
        var minute = Math.Clamp(minuteOfDay, 0, (23 * 60) + 59) % 60;
        return $"{hour:00}:{minute:00}";
    }

    /// <summary>Next Philippine clock time at or after utcNow, stored as UTC.</summary>
    public static DateTime NextFireUtc(int minuteOfDay, DateTime utcNow)
    {
        var minute = Math.Clamp(minuteOfDay, 0, (23 * 60) + 59);
        var ph = PhilippineTime.ToPh(utcNow);
        var candidate = new DateTime(ph.Year, ph.Month, ph.Day, minute / 60, minute % 60, 0);
        if (candidate < ph.AddSeconds(-90))
        {
            candidate = candidate.AddDays(1);
        }

        return PhilippineTime.ToUtc(candidate);
    }

    public async Task<int> SendDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueIds = await db.RiderNotices.AsNoTracking()
            .Where(x => x.IsActive && x.CancelledAtUtc == null && x.ScheduledAtUtc <= now)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var id in dueIds)
        {
            if (await DeliverAsync(id, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    public async Task<bool> DeliverAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var row = await db.RiderNotices.AsNoTracking()
            .Where(x => x.Id == noticeId && x.IsActive && x.CancelledAtUtc == null && x.ScheduledAtUtc <= now)
            .Select(x => new { x.OperatorId, x.ScheduledAtUtc, x.NotifyMinuteOfDay })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return false;
        }

        var next = NextFireUtc(row.NotifyMinuteOfDay, now.AddMinutes(2));
        var claimed = await db.RiderNotices
            .Where(x => x.Id == noticeId
                && x.IsActive
                && x.CancelledAtUtc == null
                && x.ScheduledAtUtc == row.ScheduledAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.SentAtUtc, now)
                    .SetProperty(x => x.ScheduledAtUtc, next)
                    .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        var riderIds = await db.RiderProfiles.AsNoTracking()
            .Where(x => x.OperatorId == row.OperatorId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await live.RiderNoticeAsync(riderIds, cancellationToken);
        return true;
    }
}
