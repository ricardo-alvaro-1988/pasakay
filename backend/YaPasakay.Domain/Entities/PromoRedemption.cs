using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class PromoRedemption : BaseEntity
{
    public Guid PromoId { get; set; }
    public OperatorPromo Promo { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public CustomerProfile Customer { get; set; } = null!;
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public DateTime RedeemedAtUtc { get; set; }
}
