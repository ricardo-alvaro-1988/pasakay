using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class PabiliOrderItemAddon : BaseEntity
{
    public Guid OrderItemId { get; set; }
    public PabiliOrderItem OrderItem { get; set; } = null!;
    public Guid? AddonOptionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitBasePrice { get; set; }
    public decimal UnitSellingPrice { get; set; }
    public decimal LineBaseTotal { get; set; }
    public decimal LineSellingTotal { get; set; }
}
