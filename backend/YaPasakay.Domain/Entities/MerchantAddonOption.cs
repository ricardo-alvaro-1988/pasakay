using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class MerchantAddonOption : BaseEntity
{
    public Guid AddonGroupId { get; set; }
    public MerchantAddonGroup AddonGroup { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Cost / acquisition price for the add-on option.</summary>
    public decimal BasePrice { get; set; }
    /// <summary>Extra amount charged to the customer when selected.</summary>
    public decimal SellingPrice { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
