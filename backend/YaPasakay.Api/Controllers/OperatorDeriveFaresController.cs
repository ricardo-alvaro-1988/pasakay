using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/derive-fares")]
public class OperatorDeriveFaresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DeriveFareZoneListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.DeriveFareZones
            .AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(new DeriveFareZoneListResponse(rows.Select(z => new DeriveFareZoneListItem(
            z.Id,
            z.Name,
            z.MaxDropoffKm,
            z.IsActive,
            z.Priority,
            GeoPolygon.Parse(z.PolygonJson).Count,
            DateTime.SpecifyKind(z.CreatedAtUtc, DateTimeKind.Utc))).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeriveFareZoneDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var zone = await LoadZoneAsync(op.Id, id, cancellationToken);
        return zone is null ? NotFound(new { message = "Derive fare zone not found." }) : Ok(MapDetail(op, zone));
    }

    [HttpPost]
    public async Task<ActionResult<DeriveFareZoneDetailResponse>> Create(
        [FromBody] SaveDeriveFareZoneRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request, op);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var zone = new DeriveFareZone
        {
            OperatorId = op.Id,
            Name = request.Name.Trim(),
            MaxDropoffKm = FareCommissionSplit.Round(request.MaxDropoffKm),
            IsActive = request.IsActive,
            Priority = request.Priority,
            PolygonJson = GeoPolygon.Serialize(request.Polygon.Select(p => new LatLngPoint(p.Lat, p.Lng))),
        };
        db.DeriveFareZones.Add(zone);
        db.DeriveFareMatrices.Add(BuildMatrix(zone.Id, VehicleType.Motorcycle, SinglePassengerRates(request.Motorcycle)));
        db.DeriveFareMatrices.Add(BuildMatrix(zone.Id, VehicleType.Tricycle, request.Tricycle));
        await db.SaveChangesAsync(cancellationToken);

        var saved = await LoadZoneAsync(op.Id, zone.Id, cancellationToken);
        return Ok(MapDetail(op, saved!));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeriveFareZoneDetailResponse>> Update(
        Guid id,
        [FromBody] SaveDeriveFareZoneRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request, op);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var zone = await LoadZoneAsync(op.Id, id, cancellationToken);
        if (zone is null)
        {
            return NotFound(new { message = "Derive fare zone not found." });
        }

        zone.Name = request.Name.Trim();
        zone.MaxDropoffKm = FareCommissionSplit.Round(request.MaxDropoffKm);
        zone.IsActive = request.IsActive;
        zone.Priority = request.Priority;
        zone.PolygonJson = GeoPolygon.Serialize(request.Polygon.Select(p => new LatLngPoint(p.Lat, p.Lng)));
        zone.UpdatedAtUtc = DateTime.UtcNow;

        await PersistRatesAsync(EnsureMatrix(zone, VehicleType.Motorcycle), SinglePassengerRates(request.Motorcycle), cancellationToken);
        await PersistRatesAsync(EnsureMatrix(zone, VehicleType.Tricycle), request.Tricycle, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var saved = await LoadZoneAsync(op.Id, zone.Id, cancellationToken);
        return Ok(MapDetail(op, saved!));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<DeriveFareZoneDetailResponse>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var zone = await LoadZoneAsync(op.Id, id, cancellationToken);
        if (zone is null)
        {
            return NotFound(new { message = "Derive fare zone not found." });
        }

        zone.IsActive = !zone.IsActive;
        zone.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(op, zone));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var zone = await db.DeriveFareZones.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (zone is null)
        {
            return NotFound(new { message = "Derive fare zone not found." });
        }

        var used = await db.Trips.AnyAsync(x => x.DeriveFareZoneId == id, cancellationToken);
        if (used)
        {
            return BadRequest(new { message = "This zone is used on past bookings. Deactivate it instead of deleting." });
        }

        db.DeriveFareZones.Remove(zone);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    static string? Validate(SaveDeriveFareZoneRequest request, Operator op)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.MaxDropoffKm <= 0)
        {
            return "Max drop-off km must be greater than zero.";
        }

        if (request.Polygon is null || request.Polygon.Count < 3)
        {
            return "Draw a polygon with at least 3 points.";
        }

        if (InvalidRates(request.Motorcycle) || InvalidRates(request.Tricycle))
        {
            return "Fare amounts cannot be negative, and passenger tiers must use unique person counts of 1 or more.";
        }

        var motorcycleError = FareCommissionSplit.Validate(
            op.MotorcycleCommissionPercent,
            request.Motorcycle.OperatorCommissionPercent,
            request.Motorcycle.DriverCommissionPercent);
        if (motorcycleError is not null)
        {
            return $"Motorcycle: {motorcycleError}";
        }

        var tricycleError = FareCommissionSplit.Validate(
            op.TricycleCommissionPercent,
            request.Tricycle.OperatorCommissionPercent,
            request.Tricycle.DriverCommissionPercent);
        if (tricycleError is not null)
        {
            return $"Tricycle: {tricycleError}";
        }

        return null;
    }

    static bool InvalidRates(FareVehicleRatesBody rates)
    {
        if (rates.BaseFare < 0 || rates.PerKm < 0 || rates.MinimumFare < 0 || rates.IncludedKm < 0)
        {
            return true;
        }

        var tiers = rates.PassengerTiers ?? [];
        if (tiers.Count == 0)
        {
            return false;
        }

        return tiers.Any(x => x.PassengerCount < 1 || x.BaseFare < 0 || x.PerKm < 0 || x.MinimumFare < 0 || x.IncludedKm < 0)
            || tiers.Select(x => x.PassengerCount).Distinct().Count() != tiers.Count;
    }

    static FareVehicleRatesBody SinglePassengerRates(FareVehicleRatesBody rates)
    {
        var tier = (rates.PassengerTiers ?? []).OrderBy(x => x.PassengerCount).FirstOrDefault()
            ?? new FarePassengerTierBody(1, rates.BaseFare, rates.PerKm, rates.MinimumFare, rates.IncludedKm);
        return rates with
        {
            BaseFare = tier.BaseFare,
            PerKm = tier.PerKm,
            MinimumFare = tier.MinimumFare,
            IncludedKm = tier.IncludedKm,
            PassengerTiers =
            [
                new FarePassengerTierBody(1, tier.BaseFare, tier.PerKm, tier.MinimumFare, tier.IncludedKm)
            ]
        };
    }

    static DeriveFareMatrix BuildMatrix(Guid zoneId, VehicleType vehicleType, FareVehicleRatesBody rates)
    {
        var primary = (rates.PassengerTiers ?? []).OrderBy(x => x.PassengerCount).FirstOrDefault()
            ?? new FarePassengerTierBody(1, rates.BaseFare, rates.PerKm, rates.MinimumFare, rates.IncludedKm);
        var matrix = new DeriveFareMatrix
        {
            DeriveFareZoneId = zoneId,
            VehicleType = vehicleType,
            BaseFare = FareCommissionSplit.Round(primary.BaseFare),
            PerKm = FareCommissionSplit.Round(primary.PerKm),
            MinimumFare = FareCommissionSplit.Round(primary.MinimumFare),
            IncludedKm = FareCommissionSplit.Round(primary.IncludedKm),
            OperatorCommissionPercent = FareCommissionSplit.Round(rates.OperatorCommissionPercent),
            DriverCommissionPercent = FareCommissionSplit.Round(rates.DriverCommissionPercent),
            IsActive = rates.IsActive,
        };
        foreach (var tier in (rates.PassengerTiers ?? []).OrderBy(x => x.PassengerCount))
        {
            matrix.PassengerTiers.Add(new DeriveFarePassengerTier
            {
                PassengerCount = tier.PassengerCount,
                BaseFare = FareCommissionSplit.Round(tier.BaseFare),
                PerKm = FareCommissionSplit.Round(tier.PerKm),
                MinimumFare = FareCommissionSplit.Round(tier.MinimumFare),
                IncludedKm = FareCommissionSplit.Round(tier.IncludedKm),
            });
        }

        if (matrix.PassengerTiers.Count == 0)
        {
            matrix.PassengerTiers.Add(new DeriveFarePassengerTier
            {
                PassengerCount = 1,
                BaseFare = matrix.BaseFare,
                PerKm = matrix.PerKm,
                MinimumFare = matrix.MinimumFare,
                IncludedKm = matrix.IncludedKm,
            });
        }

        return matrix;
    }

    static DeriveFareMatrix EnsureMatrix(DeriveFareZone zone, VehicleType vehicleType)
    {
        var existing = zone.Matrices.FirstOrDefault(x => x.VehicleType == vehicleType);
        if (existing is not null)
        {
            return existing;
        }

        var created = new DeriveFareMatrix
        {
            DeriveFareZoneId = zone.Id,
            VehicleType = vehicleType,
            IsActive = true,
        };
        zone.Matrices.Add(created);
        return created;
    }

    async Task PersistRatesAsync(DeriveFareMatrix matrix, FareVehicleRatesBody rates, CancellationToken cancellationToken)
    {
        var tiers = (rates.PassengerTiers ?? [])
            .OrderBy(x => x.PassengerCount)
            .ToList();
        if (tiers.Count == 0)
        {
            tiers.Add(new FarePassengerTierBody(1, rates.BaseFare, rates.PerKm, rates.MinimumFare, rates.IncludedKm));
        }

        var primary = tiers[0];
        matrix.BaseFare = FareCommissionSplit.Round(primary.BaseFare);
        matrix.PerKm = FareCommissionSplit.Round(primary.PerKm);
        matrix.MinimumFare = FareCommissionSplit.Round(primary.MinimumFare);
        matrix.IncludedKm = FareCommissionSplit.Round(primary.IncludedKm);
        matrix.OperatorCommissionPercent = FareCommissionSplit.Round(rates.OperatorCommissionPercent);
        matrix.DriverCommissionPercent = FareCommissionSplit.Round(rates.DriverCommissionPercent);
        matrix.IsActive = rates.IsActive;
        matrix.UpdatedAtUtc = DateTime.UtcNow;

        var existing = matrix.PassengerTiers.ToList();
        db.DeriveFarePassengerTiers.RemoveRange(existing);
        matrix.PassengerTiers.Clear();
        foreach (var tier in tiers)
        {
            matrix.PassengerTiers.Add(new DeriveFarePassengerTier
            {
                PassengerCount = tier.PassengerCount,
                BaseFare = FareCommissionSplit.Round(tier.BaseFare),
                PerKm = FareCommissionSplit.Round(tier.PerKm),
                MinimumFare = FareCommissionSplit.Round(tier.MinimumFare),
                IncludedKm = FareCommissionSplit.Round(tier.IncludedKm),
            });
        }

        await Task.CompletedTask;
    }

    async Task<DeriveFareZone?> LoadZoneAsync(Guid operatorId, Guid id, CancellationToken cancellationToken) =>
        await db.DeriveFareZones
            .Include(x => x.Matrices)
            .ThenInclude(x => x.PassengerTiers)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);

    static DeriveFareZoneDetailResponse MapDetail(Operator op, DeriveFareZone zone)
    {
        var polygon = GeoPolygon.Parse(zone.PolygonJson)
            .Select(p => new DeriveFareLatLng(p.Lat, p.Lng))
            .ToList();
        return new DeriveFareZoneDetailResponse(
            zone.Id,
            zone.Name,
            zone.MaxDropoffKm,
            zone.IsActive,
            zone.Priority,
            polygon,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            MapRates(zone.Matrices.FirstOrDefault(x => x.VehicleType == VehicleType.Motorcycle)),
            MapRates(zone.Matrices.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle)));
    }

    static DeriveFareRatesItem? MapRates(DeriveFareMatrix? matrix)
    {
        if (matrix is null)
        {
            return null;
        }

        var tiers = (matrix.PassengerTiers ?? [])
            .OrderBy(x => x.PassengerCount)
            .Select(x => new FarePassengerTierItem(
                x.PassengerCount,
                x.BaseFare,
                x.PerKm,
                x.MinimumFare,
                x.IncludedKm))
            .ToList();
        if (tiers.Count == 0)
        {
            tiers.Add(new FarePassengerTierItem(1, matrix.BaseFare, matrix.PerKm, matrix.MinimumFare, matrix.IncludedKm));
        }

        var sampleCount = matrix.VehicleType == VehicleType.Motorcycle ? 1 : Math.Max(1, tiers[0].PassengerCount);
        return new DeriveFareRatesItem(
            matrix.VehicleType,
            matrix.BaseFare,
            matrix.PerKm,
            matrix.MinimumFare,
            matrix.IncludedKm,
            matrix.OperatorCommissionPercent,
            matrix.DriverCommissionPercent,
            matrix.IsActive,
            tiers,
            FareQuote.SamplesForPassengers(matrix, sampleCount));
    }
}
