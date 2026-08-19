using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class RiderProfile : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleModel { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string? ProfilePhotoPath { get; set; }
    public string? LicensePhotoPath { get; set; }
    public Guid? AddressBarangayId { get; set; }
    public Barangay? AddressBarangay { get; set; }
    public string AddressDetails { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public double? LastLat { get; set; }
    public double? LastLng { get; set; }
    public DateTime? LastLocationAtUtc { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? OnlineAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public ICollection<TripOffer> Offers { get; set; } = new List<TripOffer>();
    public ICollection<RiderPaymentMethod> PaymentMethods { get; set; } = new List<RiderPaymentMethod>();
    public RiderWallet? Wallet { get; set; }
}
