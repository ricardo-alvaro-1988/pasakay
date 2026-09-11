using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Operator-scoped delivery fare settings for Pabili.</summary>
public class PabiliMatrix : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public decimal BaseFareAmount { get; set; }
    /// <summary>Included kilometers covered by the base fare.</summary>
    public decimal KmScope { get; set; } = 1;
    /// <summary>Rate charged for each km after the scope.</summary>
    public decimal SucceedingKm { get; set; }
    public decimal FareOperatorCommissionPercent { get; set; }
    public decimal FareRiderCommissionPercent { get; set; }
    public decimal MarkupOperatorCommissionPercent { get; set; }
    public decimal MarkupRiderCommissionPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PabiliSurcharge> Surcharges { get; set; } = new List<PabiliSurcharge>();
}
