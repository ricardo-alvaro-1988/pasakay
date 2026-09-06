using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class OperatorPromo : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    /// <summary>Uppercase Save{N}, e.g. SAVE10.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>1–100 percent off the fare.</summary>
    public int DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public ICollection<PromoRedemption> Redemptions { get; set; } = new List<PromoRedemption>();
}
