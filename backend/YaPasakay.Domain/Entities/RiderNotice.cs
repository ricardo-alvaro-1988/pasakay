using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class RiderNotice : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
