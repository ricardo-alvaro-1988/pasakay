using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public AppUser? Actor { get; set; }
    public AuditAction Action { get; set; }
    public string Summary { get; set; } = string.Empty;
}
