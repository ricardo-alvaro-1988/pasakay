using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class MerchantProductCategory : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MerchantProduct> Products { get; set; } = new List<MerchantProduct>();
}
