using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class VehicleCategory : BaseEntity
{
    /// <summary>Null = platform preset; set = operator-owned custom type.</summary>
    public Guid? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxPassengers { get; set; } = 1;
    public string IconKey { get; set; } = "generic";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsCargo { get; set; }
    /// <summary>Legacy VehicleType enum int (1=Motorcycle, 2=Tricycle, …).</summary>
    public int? LegacyEnumValue { get; set; }
    public ICollection<OperatorVehicleOffer> Offers { get; set; } = new List<OperatorVehicleOffer>();
}
