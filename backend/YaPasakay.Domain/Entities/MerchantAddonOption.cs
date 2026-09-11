using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class MerchantAddonOption : BaseEntity
{
    public Guid AddonGroupId { get; set; }
    public MerchantAddonGroup AddonGroup { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Extra amount added to the product selling price when selected.</summary>
    public decimal PriceDelta { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
