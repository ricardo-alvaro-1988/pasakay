using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class OperatorNotification : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid? BillId { get; set; }
    public OperatorBill? Bill { get; set; }
    public NotificationKind Kind { get; set; } = NotificationKind.Billing;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? ReadAtUtc { get; set; }
}
