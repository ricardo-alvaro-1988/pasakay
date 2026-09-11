using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Adopts a merchant library add-on group onto a product.</summary>
public class MerchantProductAddon : BaseEntity
{
    public Guid ProductId { get; set; }
    public MerchantProduct Product { get; set; } = null!;
    public Guid AddonGroupId { get; set; }
    public MerchantAddonGroup AddonGroup { get; set; } = null!;
    public int SortOrder { get; set; }
}
