using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class FareMatrix : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public decimal BaseFare { get; set; }
    public decimal PerKm { get; set; }
    public decimal MinimumFare { get; set; }
    public decimal IncludedKm { get; set; } = 1;
    public decimal OperatorCommissionPercent { get; set; }
    public decimal DriverCommissionPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<FareSurcharge> Surcharges { get; set; } = new List<FareSurcharge>();
}
