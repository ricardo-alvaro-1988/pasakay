using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class DeriveFareZone : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Max allowed driving distance (km) from pickup to drop-off.</summary>
    public decimal MaxDropoffKm { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Higher wins when pickup sits in overlapping zones.</summary>
    public int Priority { get; set; }
    /// <summary>JSON array of { "lat": number, "lng": number } ring points.</summary>
    public string PolygonJson { get; set; } = "[]";
    public ICollection<DeriveFareMatrix> Matrices { get; set; } = new List<DeriveFareMatrix>();
}
