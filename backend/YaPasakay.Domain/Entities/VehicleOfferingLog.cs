using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

/// <summary>
/// Append-only log of fare-matrix Offered / Not offered toggles (multiple rows per operator/vehicle).
/// </summary>
public class VehicleOfferingLog : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? VehicleCategoryId { get; set; }
    public VehicleCategory? VehicleCategory { get; set; }
    public VehicleType VehicleType { get; set; }
    public string VehicleCode { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    /// <summary>True when set to Offered; false when set to Not offered.</summary>
    public bool IsOffered { get; set; }
    public Guid? ActorUserId { get; set; }
    public AppUser? Actor { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public bool AcceptedTerms { get; set; }
    public string? TermsVersion { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
