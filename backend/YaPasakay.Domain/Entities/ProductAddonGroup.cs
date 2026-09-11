using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class ProductAddonGroup : BaseEntity
{
    public Guid ProductId { get; set; }
    public MerchantProduct Product { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProductAddonOption> Options { get; set; } = new List<ProductAddonOption>();
}
