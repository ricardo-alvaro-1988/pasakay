using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class PabiliOrderOffer : BaseEntity
{
    public Guid OrderId { get; set; }
    public PabiliOrder Order { get; set; } = null!;
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
    public OfferStatus Status { get; set; } = OfferStatus.Offered;
    public decimal? DistanceKm { get; set; }
    public DateTime OfferedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}
