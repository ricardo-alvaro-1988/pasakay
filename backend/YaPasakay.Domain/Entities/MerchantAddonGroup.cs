using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Merchant-level add-on group library (adopted by products).</summary>
public class MerchantAddonGroup : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MerchantAddonOption> Options { get; set; } = new List<MerchantAddonOption>();
    public ICollection<MerchantProductAddon> ProductLinks { get; set; } = new List<MerchantProductAddon>();
}
