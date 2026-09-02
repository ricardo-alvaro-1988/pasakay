using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class FarePassengerTier : BaseEntity
{
    public Guid FareMatrixId { get; set; }
    public FareMatrix FareMatrix { get; set; } = null!;
    public int PassengerCount { get; set; } = 1;
    public decimal BaseFare { get; set; }
    public decimal PerKm { get; set; }
    public decimal MinimumFare { get; set; }
    public decimal IncludedKm { get; set; } = 1;
}
