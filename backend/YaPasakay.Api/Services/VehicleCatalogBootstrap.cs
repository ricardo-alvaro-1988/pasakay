using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class VehicleCatalogBootstrap
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformCategoriesAsync(db, cancellationToken);
        await EnsureOperatorOffersAsync(db, cancellationToken);
        await BackfillVehicleCategoryIdsAsync(db, cancellationToken);
    }

    public static async Task EnsurePlatformCategoriesAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.VehicleCategories
            .Where(x => x.OperatorId == null)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var preset in VehicleCatalog.PlatformPresets)
        {
            if (existingSet.Contains(preset.Id)) continue;
            db.VehicleCategories.Add(new VehicleCategory
            {
                Id = preset.Id,
                OperatorId = null,
                Code = preset.Code,
                Name = preset.Name,
                MaxPassengers = preset.MaxPassengers,
                IconKey = preset.IconKey,
                IsCargo = preset.IsCargo,
                SortOrder = preset.SortOrder,
                IsActive = true,
                LegacyEnumValue = (int)preset.LegacyEnum,
                CreatedAtUtc = now,
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task EnsureOperatorOffersAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var operators = await db.Operators
            .Select(x => new { x.Id, x.MotorcycleCommissionPercent, x.TricycleCommissionPercent })
            .ToListAsync(cancellationToken);
        if (operators.Count == 0) return;

        var existing = await db.OperatorVehicleOffers
            .Select(x => new { x.OperatorId, x.VehicleCategoryId })
            .ToListAsync(cancellationToken);
        var existingSet = existing.Select(x => (x.OperatorId, x.VehicleCategoryId)).ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var op in operators)
        {
            foreach (var preset in VehicleCatalog.PlatformPresets)
            {
                if (existingSet.Contains((op.Id, preset.Id))) continue;
                var commission = preset.LegacyEnum switch
                {
                    VehicleType.Motorcycle => op.MotorcycleCommissionPercent,
                    VehicleType.Tricycle => op.TricycleCommissionPercent,
                    _ => preset.DefaultCommissionPercent,
                };
                db.OperatorVehicleOffers.Add(new OperatorVehicleOffer
                {
                    Id = Guid.NewGuid(),
                    OperatorId = op.Id,
                    VehicleCategoryId = preset.Id,
                    IsEnabled = preset.DefaultEnabled,
                    CommissionPercent = commission,
                    CreatedAtUtc = now,
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task BackfillVehicleCategoryIdsAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var preset in VehicleCatalog.PlatformPresets)
        {
            var type = (int)preset.LegacyEnum;
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE RiderProfiles SET VehicleCategoryId = {preset.Id}
                WHERE VehicleCategoryId IS NULL AND VehicleType = {type}", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Trips SET VehicleCategoryId = {preset.Id}
                WHERE VehicleCategoryId IS NULL AND VehicleType = {type}", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE FareMatrices SET VehicleCategoryId = {preset.Id}
                WHERE VehicleCategoryId IS NULL AND VehicleType = {type}", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE DeriveFareMatrices SET VehicleCategoryId = {preset.Id}
                WHERE VehicleCategoryId IS NULL AND VehicleType = {type}", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE RiderApplications SET VehicleCategoryId = {preset.Id}
                WHERE VehicleCategoryId IS NULL AND VehicleType = {type}", cancellationToken);
        }
    }

    public static void ApplyCategory(RiderProfile rider, VehicleType type)
    {
        rider.VehicleType = type;
        rider.VehicleCategoryId = VehicleCatalog.IdFor(type);
    }

    public static void ApplyCategory(Trip trip, VehicleType type)
    {
        trip.VehicleType = type;
        trip.VehicleCategoryId = VehicleCatalog.IdFor(type);
    }

    public static void ApplyCategory(FareMatrix fare, VehicleType type)
    {
        fare.VehicleType = type;
        fare.VehicleCategoryId = VehicleCatalog.IdFor(type);
    }

    public static void ApplyCategory(DeriveFareMatrix fare, VehicleType type)
    {
        fare.VehicleType = type;
        fare.VehicleCategoryId = VehicleCatalog.IdFor(type);
    }

    public static void ApplyCategory(RiderApplication app, VehicleType type)
    {
        app.VehicleType = type;
        app.VehicleCategoryId = VehicleCatalog.IdFor(type);
    }
}
