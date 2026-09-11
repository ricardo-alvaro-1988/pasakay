using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class PabiliOrder : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public CustomerProfile Customer { get; set; } = null!;
    public Guid? RiderId { get; set; }
    public RiderProfile? Rider { get; set; }
    public string Reference { get; set; } = string.Empty;
    public PabiliOrderStatus Status { get; set; } = PabiliOrderStatus.Pending;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public double PickupLat { get; set; }
    public double PickupLng { get; set; }
    public string DropoffAddress { get; set; } = string.Empty;
    public double DropoffLat { get; set; }
    public double DropoffLng { get; set; }
    public Guid? DropoffBarangayId { get; set; }
    public Barangay? DropoffBarangay { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal GoodsSubtotal { get; set; }
    public decimal GoodsBaseSubtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal SurchargeTotal { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public string AdjustmentLabel { get; set; } = string.Empty;
    public decimal CustomerTotal { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    /// <summary>Customer-entered wallet payment reference (GCash / Maya / Other).</summary>
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    public DateTime? DeliveringAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public CancelledBy CancelledBy { get; set; } = CancelledBy.None;
    public string? CancelReason { get; set; }
    public decimal? FareSystemPercent { get; set; }
    public decimal? FareOperatorPercent { get; set; }
    public decimal? FareRiderPercent { get; set; }
    public decimal? MarkupSystemPercent { get; set; }
    public decimal? MarkupOperatorPercent { get; set; }
    public decimal? MarkupRiderPercent { get; set; }
    public decimal? FareSystemAmount { get; set; }
    public decimal? FareOperatorAmount { get; set; }
    public decimal? FareRiderAmount { get; set; }
    public decimal? MarkupSystemAmount { get; set; }
    public decimal? MarkupOperatorAmount { get; set; }
    public decimal? MarkupRiderAmount { get; set; }
    public ICollection<PabiliOrderItem> Items { get; set; } = new List<PabiliOrderItem>();
    public ICollection<PabiliOrderOffer> Offers { get; set; } = new List<PabiliOrderOffer>();
}
