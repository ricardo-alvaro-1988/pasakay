using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class RiderWalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public RiderWallet Wallet { get; set; } = null!;
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
    public WalletTransactionKind Kind { get; set; }
    public WalletTransactionStatus Status { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal? BalanceAfter { get; set; }
    public Guid? TripId { get; set; }
    public Trip? Trip { get; set; }
    public string? Note { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public AppUser? ResolvedByUser { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
