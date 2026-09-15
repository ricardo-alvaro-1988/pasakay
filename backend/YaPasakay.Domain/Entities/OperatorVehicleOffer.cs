using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class OperatorVehicleOffer : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid VehicleCategoryId { get; set; }
    public VehicleCategory VehicleCategory { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public decimal CommissionPercent { get; set; }
    public string? DisplayName { get; set; }
    public int? MaxPassengers { get; set; }
}
