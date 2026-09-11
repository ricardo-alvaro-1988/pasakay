using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class MerchantProduct : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public MerchantProductCategory? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool AvailableOnStorefront { get; set; } = true;
    public int SortOrder { get; set; }
    public string? ImagePath { get; set; }
    public ICollection<ProductAddonGroup> AddonGroups { get; set; } = new List<ProductAddonGroup>();
}
