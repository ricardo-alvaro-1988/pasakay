using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class SupportTicket : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid? TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid? CustomerId { get; set; }
    public CustomerProfile? Customer { get; set; }
    public Guid? RiderId { get; set; }
    public RiderProfile? Rider { get; set; }
    public SupportKind Kind { get; set; } = SupportKind.Support;
    public SupportStatus Status { get; set; } = SupportStatus.Open;
    public SupportOpenedBy OpenedBy { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? OperatorNotes { get; set; }
    public double? SosLat { get; set; }
    public double? SosLng { get; set; }
    public DateTime? SosAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}
