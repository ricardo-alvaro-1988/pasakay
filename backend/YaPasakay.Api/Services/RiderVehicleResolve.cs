using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

/// <summary>Resolve rider VehicleType + VehicleCategoryId from optional category (dual-write).</summary>
public static class RiderVehicleResolve
{
    public static async Task<(VehicleType Type, Guid CategoryId, string? Error)> ResolveAsync(
        AppDbContext db,
        Guid operatorId,
        Guid? vehicleCategoryId,
        VehicleType vehicleType,
        CancellationToken cancellationToken,
        bool requireEnabled = true)
    {
        if (vehicleCategoryId is Guid categoryId)
        {
            var offer = await db.OperatorVehicleOffers
                .AsNoTracking()
                .Include(x => x.VehicleCategory)
                .FirstOrDefaultAsync(
                    x => x.OperatorId == operatorId
                        && x.VehicleCategoryId == categoryId
                        && x.VehicleCategory != null
                        && x.VehicleCategory.IsActive
                        && (x.VehicleCategory.OperatorId == null || x.VehicleCategory.OperatorId == operatorId),
                    cancellationToken);
            if (offer?.VehicleCategory is null)
            {
                return (default, default, "Choose a valid vehicle for this operator.");
            }

            // Prefer enabled offers; still allow an existing assignment if the offer was turned off.
            if (!offer.IsEnabled && requireEnabled)
            {
                return (default, default, "Choose a valid vehicle for this operator.");
            }

            var type = TypeFromCategory(offer.VehicleCategory);
            if (type is null)
            {
                return (default, default, "Choose a valid vehicle for this operator.");
            }

            return (type.Value, offer.VehicleCategory.Id, null);
        }

        if (vehicleType == VehicleType.Custom)
        {
            return (default, default, "Custom vehicles require a vehicle category.");
        }

        if (VehicleTypeRules.ValidateChoice(vehicleType) is { } vehicleError)
        {
            return (default, default, vehicleError);
        }

        return (vehicleType, VehicleCatalog.IdFor(vehicleType), null);
    }

    public static VehicleType? TypeFromCategory(VehicleCategory cat)
    {
        if (cat.OperatorId is not null)
        {
            return VehicleType.Custom;
        }

        if (cat.LegacyEnumValue is int v && Enum.IsDefined(typeof(VehicleType), v) && v != (int)VehicleType.Custom)
        {
            return (VehicleType)v;
        }

        return VehicleCatalog.TypeFor(cat.Id);
    }

    public static async Task<IReadOnlyList<(Guid CategoryId, string Code, string Name, string VehicleType, bool IsCustom)>> EnabledOffersAsync(
        AppDbContext db,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        await VehicleCatalogBootstrap.EnsureAsync(db, cancellationToken);
        var rows = await db.OperatorVehicleOffers
            .AsNoTracking()
            .Include(x => x.VehicleCategory)
            .Where(x => x.OperatorId == operatorId
                && x.IsEnabled
                && x.VehicleCategory != null
                && x.VehicleCategory.IsActive
                && (x.VehicleCategory.OperatorId == null || x.VehicleCategory.OperatorId == operatorId))
            .OrderBy(x => x.VehicleCategory!.SortOrder)
            .ThenBy(x => x.VehicleCategory!.Name)
            .ToListAsync(cancellationToken);

        return rows
            .Select(o =>
            {
                var cat = o.VehicleCategory!;
                var type = TypeFromCategory(cat) ?? VehicleType.Custom;
                var isCustom = cat.OperatorId is not null;
                return (
                    cat.Id,
                    cat.Code,
                    o.DisplayName ?? cat.Name,
                    type.ToString(),
                    isCustom);
            })
            .ToList();
    }
}
