using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class CustomerLoginBlock : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid BlockedByUserId { get; set; }
    public AppUser BlockedByUser { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTime? LiftedAtUtc { get; set; }
}
