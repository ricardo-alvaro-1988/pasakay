using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class AdminNotification : BaseEntity
{
    public NotificationKind Kind { get; set; } = NotificationKind.Sos;
    public Guid? SupportTicketId { get; set; }
    public SupportTicket? SupportTicket { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? ReadAtUtc { get; set; }
}
