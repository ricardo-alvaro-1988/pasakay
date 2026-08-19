using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class RiderPaymentMethod : BaseEntity
{
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
    public PaymentMethod Method { get; set; }
}
