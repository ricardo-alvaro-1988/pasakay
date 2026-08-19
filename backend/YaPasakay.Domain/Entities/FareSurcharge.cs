using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class FareSurcharge : BaseEntity
{
    public Guid FareMatrixId { get; set; }
    public FareMatrix FareMatrix { get; set; } = null!;
    public SurchargeKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TimeOnly? WindowStart { get; set; }
    public TimeOnly? WindowEnd { get; set; }
    public DateTime? RangeStartUtc { get; set; }
    public DateTime? RangeEndUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
