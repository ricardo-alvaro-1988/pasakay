using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

/// <summary>
/// Fare matrix lookup keyed by VehicleCategoryId when present, with VehicleType fallback.
/// </summary>
public sealed class FareMatrixLookup
{
    private readonly Dictionary<(Guid OperatorId, Guid CategoryId, Guid MunicipalityId), FareMatrix> _byCategory;
    private readonly Dictionary<(Guid OperatorId, VehicleType VehicleType, Guid MunicipalityId), FareMatrix> _byType;

    public FareMatrixLookup(
        Dictionary<(Guid OperatorId, Guid CategoryId, Guid MunicipalityId), FareMatrix> byCategory,
        Dictionary<(Guid OperatorId, VehicleType VehicleType, Guid MunicipalityId), FareMatrix> byType)
    {
        _byCategory = byCategory;
        _byType = byType;
    }

    public static FareMatrixLookup Empty { get; } = new([], []);

    public FareMatrix? Resolve(
        Guid operatorId,
        VehicleType vehicleType,
        Guid municipalityId,
        Guid? vehicleCategoryId)
    {
        if (vehicleCategoryId is Guid categoryId
            && _byCategory.TryGetValue((operatorId, categoryId, municipalityId), out var byCategory))
        {
            return byCategory;
        }

        return _byType.TryGetValue((operatorId, vehicleType, municipalityId), out var byType)
            ? byType
            : null;
    }

    public FareMatrix? Resolve(Trip trip, Guid municipalityId) =>
        Resolve(trip.OperatorId, trip.VehicleType, municipalityId, trip.VehicleCategoryId);
}
