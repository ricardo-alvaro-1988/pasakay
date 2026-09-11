using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class ProductAddonOption : BaseEntity
{
    public Guid AddonGroupId { get; set; }
    public ProductAddonGroup AddonGroup { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
