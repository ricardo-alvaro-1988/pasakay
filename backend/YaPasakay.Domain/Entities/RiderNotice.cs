using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class RiderNotice : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Minutes after midnight in Philippine time. 15:00 is 900.</summary>
    public int NotifyMinuteOfDay { get; set; }
    /// <summary>Next time this daily announcement should notify riders.</summary>
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
