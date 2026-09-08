using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class RiderInviteLink : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public RiderInviteKind Kind { get; set; } = RiderInviteKind.Rotating;
    public bool IsActive { get; set; } = true;
}

public class RiderApplication : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid InviteLinkId { get; set; }
    public RiderInviteLink InviteLink { get; set; } = null!;
    public RiderApplicationStatus Status { get; set; } = RiderApplicationStatus.Pending;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleFranchiseNumber { get; set; } = string.Empty;
    public string? VehicleModel { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public Guid AddressBarangayId { get; set; }
    public Barangay AddressBarangay { get; set; } = null!;
    public string AddressDetails { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public string? ProfilePhotoPath { get; set; }
    public string? LicensePhotoPath { get; set; }
    public string AcceptedPaymentMethods { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? RiderProfileId { get; set; }
}
