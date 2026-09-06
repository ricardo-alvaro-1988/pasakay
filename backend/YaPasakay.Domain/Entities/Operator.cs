using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class Operator : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public Guid? AddressBarangayId { get; set; }
    public Barangay? AddressBarangay { get; set; }
    public string AddressDetails { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public string AreaOfOperation { get; set; } = string.Empty;
    public string GovernmentIdType { get; set; } = string.Empty;
    public string GovernmentId { get; set; } = string.Empty;
    public string? ProfilePhotoPath { get; set; }
    public string? GovernmentIdPhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal MotorcycleCommissionPercent { get; set; } = 10;
    public decimal TricycleCommissionPercent { get; set; } = 5;
    public BookingDispatchMode BookingDispatchMode { get; set; } = BookingDispatchMode.Broadcast;
    /// <summary>Max distance (km) for broadcast offers to nearby riders.</summary>
    public double BroadcastRadiusKm { get; set; } = 5;
    public string GCashNumber { get; set; } = string.Empty;
    public string? GCashQrPath { get; set; }
    public bool GCashIsActive { get; set; } = true;
    public string MayaNumber { get; set; } = string.Empty;
    public string? MayaQrPath { get; set; }
    public bool MayaIsActive { get; set; } = true;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<RiderProfile> Riders { get; set; } = new List<RiderProfile>();
    public ICollection<OperatorBarangay> Areas { get; set; } = new List<OperatorBarangay>();
    public ICollection<FareMatrix> FareMatrices { get; set; } = new List<FareMatrix>();
    public ICollection<OperatorBill> Bills { get; set; } = new List<OperatorBill>();
    public ICollection<OperatorNotification> Notifications { get; set; } = new List<OperatorNotification>();
    public ICollection<OperatorCashInBankAccount> CashInBankAccounts { get; set; } = new List<OperatorCashInBankAccount>();
    public ICollection<OperatorPromo> Promos { get; set; } = new List<OperatorPromo>();
}
