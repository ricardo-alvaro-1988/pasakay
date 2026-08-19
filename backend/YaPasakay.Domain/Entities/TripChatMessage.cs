using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class TripChatMessage : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public ChatSender Sender { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public DateTime SentAtUtc { get; set; }
}
