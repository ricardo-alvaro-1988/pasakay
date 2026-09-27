using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Customer-saved favorite rider for one-tap rebook when online.</summary>
public class CustomerFavoriteRider : BaseEntity
{
    public Guid CustomerId { get; set; }
    public CustomerProfile Customer { get; set; } = null!;
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
}
