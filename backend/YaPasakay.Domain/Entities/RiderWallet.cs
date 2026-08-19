using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class RiderWallet : BaseEntity
{
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
    public decimal Balance { get; set; }
    public ICollection<RiderWalletTransaction> Transactions { get; set; } = new List<RiderWalletTransaction>();
}
