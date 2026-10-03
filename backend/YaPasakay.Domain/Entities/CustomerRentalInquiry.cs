using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

/// <summary>Customer request to rent a vehicle (Rental Car).</summary>
public class CustomerRentalInquiry : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public CustomerProfile Customer { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public Guid? VehicleCategoryId { get; set; }
    public DateTime ScheduleFromUtc { get; set; }
    public DateTime ScheduleToUtc { get; set; }
    public string LocationDetails { get; set; } = string.Empty;
    public double LocationLat { get; set; }
    public double LocationLng { get; set; }
    public Guid? BarangayId { get; set; }
    public string? Notes { get; set; }
    public string MobileNumber { get; set; } = string.Empty;
    public RentalLeadStatus Status { get; set; } = RentalLeadStatus.Pending;
}
