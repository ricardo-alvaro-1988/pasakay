using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool ForOperators { get; set; }
    public bool ForRiders { get; set; }
    public bool ForCustomers { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
