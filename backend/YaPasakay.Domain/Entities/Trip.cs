using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class Trip : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid RiderId { get; set; }
    public RiderProfile Rider { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public TripStatus Status { get; set; }
    public string Pickup { get; set; } = string.Empty;
    public string PickupDetails { get; set; } = string.Empty;
    public double? PickupLat { get; set; }
    public double? PickupLng { get; set; }
    public Guid? PickupBarangayId { get; set; }
    public Barangay? PickupBarangay { get; set; }
    public string Dropoff { get; set; } = string.Empty;
    public string DropoffDetails { get; set; } = string.Empty;
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }
    public Guid? DropoffBarangayId { get; set; }
    public Barangay? DropoffBarangay { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public CustomerProfile? Customer { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Fare { get; set; }
    public decimal DistanceKm { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public int? Rating { get; set; }
    public string? RatingComment { get; set; }
    public DateTime? RatedAtUtc { get; set; }
    public Guid? BillId { get; set; }
    public OperatorBill? Bill { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? PaymentMethodOther { get; set; }
    public ICollection<TripChatMessage> ChatMessages { get; set; } = new List<TripChatMessage>();
    public ICollection<TripOffer> Offers { get; set; } = new List<TripOffer>();
}
