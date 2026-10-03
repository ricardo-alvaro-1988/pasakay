using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

/// <summary>Customer offer to list their vehicle for rent (List Your Car).</summary>
public class CustomerCarListing : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public CustomerProfile Customer { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public Guid? VehicleCategoryId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public int Seater { get; set; }
    public string FrontImagePath { get; set; } = string.Empty;
    public string BackImagePath { get; set; } = string.Empty;
    public string LeftImagePath { get; set; } = string.Empty;
    public string RightImagePath { get; set; } = string.Empty;
    public string InsideImagePath { get; set; } = string.Empty;
    public bool AvailableMonday { get; set; } = true;
    public bool AvailableTuesday { get; set; } = true;
    public bool AvailableWednesday { get; set; } = true;
    public bool AvailableThursday { get; set; } = true;
    public bool AvailableFriday { get; set; } = true;
    public bool AvailableSaturday { get; set; } = true;
    public bool AvailableSunday { get; set; } = true;
    public RentalLeadStatus Status { get; set; } = RentalLeadStatus.Pending;
}
