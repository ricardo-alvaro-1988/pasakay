using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class RiderNoticeService(AppDbContext db, LiveNotify live)
{
    public async Task<int> SendDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueIds = await db.RiderNotices.AsNoTracking()
            .Where(x => x.SentAtUtc == null && x.CancelledAtUtc == null && x.ScheduledAtUtc <= now)
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
        var claimed = await db.RiderNotices
            .Where(x => x.Id == noticeId && x.SentAtUtc == null && x.CancelledAtUtc == null && x.ScheduledAtUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.SentAtUtc, now)
                    .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        var notice = await db.RiderNotices.AsNoTracking()
            .Where(x => x.Id == noticeId)
            .Select(x => new { x.OperatorId, x.Title, x.Body })
            .FirstAsync(cancellationToken);

        var riderIds = await db.RiderProfiles.AsNoTracking()
            .Where(x => x.OperatorId == notice.OperatorId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await live.RiderNoticeAsync(riderIds, cancellationToken);
        return true;
    }
}
