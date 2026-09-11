using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class PabiliOrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public PabiliOrder Order { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitBasePrice { get; set; }
    public decimal UnitSellingPrice { get; set; }
    public decimal LineBaseTotal { get; set; }
    public decimal LineSellingTotal { get; set; }
    public int SortOrder { get; set; }
    public ICollection<PabiliOrderItemAddon> Addons { get; set; } = new List<PabiliOrderItemAddon>();
}
