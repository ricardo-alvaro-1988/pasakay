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
[Route("api/operator/fares")]
public class OperatorFaresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorFareDetailResponse>> Get(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var fares = await db.FareMatrices
            .Include(x => x.Surcharges)
            .Include(x => x.PassengerTiers)
            .Where(x => x.OperatorId == op!.Id)
            .ToListAsync(cancellationToken);
        return Ok(new OperatorFareDetailResponse(
            op.Id,
            op.CompanyName,
            op.IsActive,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            OperatorMaps.FareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Motorcycle), true),
            OperatorMaps.FareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle), true)));
    }

    [HttpPut]
    public async Task<ActionResult<OperatorFareDetailResponse>> SaveRates(
        [FromBody] SaveFareRatesRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        if (request.VehicleType is not VehicleType.Motorcycle and not VehicleType.Tricycle)
        {
            return BadRequest(new { message = "Choose Motorcycle or Tricycle." });
        }

        if (request.BaseFare < 0 || request.PerKm < 0 || request.MinimumFare < 0 || request.IncludedKm < 0
            || InvalidTiers(request.PassengerTiers))
        {
            return BadRequest(new { message = "Fare amounts cannot be negative, and passenger tiers must use unique person counts of 1 or more." });
        }

        var splitError = FareCommissionSplit.Validate(
            FareCommissionSplit.SystemPercent(op, request.VehicleType),
            request.OperatorCommissionPercent,
            request.DriverCommissionPercent);
        if (splitError is not null)
        {
            return BadRequest(new { message = splitError });
        }

        var fare = await db.FareMatrices
            .Include(x => x.PassengerTiers)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.VehicleType == request.VehicleType, cancellationToken);
        if (fare is null)
        {
            fare = new FareMatrix
            {
                OperatorId = op.Id,
                VehicleType = request.VehicleType
            };
            db.FareMatrices.Add(fare);
        }

        ApplyRates(fare, request);
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpPut("matrix")]
    public async Task<ActionResult<OperatorFareDetailResponse>> SaveRelated(
        [FromBody] SaveRelatedFareRatesRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        if (InvalidRates(request.Motorcycle) || InvalidRates(request.Tricycle))
        {
            return BadRequest(new { message = "Fare amounts cannot be negative, and passenger tiers must use unique person counts of 1 or more." });
        }

        if ((request.Motorcycle.PassengerTiers?.Count ?? 0) == 0 && request.Motorcycle.BaseFare < 0)
        {
            return BadRequest(new { message = "Add at least one passenger tier for Motorcycle." });
        }

        var motorcycleError = FareCommissionSplit.Validate(
            op.MotorcycleCommissionPercent,
            request.Motorcycle.OperatorCommissionPercent,
            request.Motorcycle.DriverCommissionPercent);
        if (motorcycleError is not null)
        {
            return BadRequest(new { message = $"Motorcycle: {motorcycleError}" });
        }

        var tricycleError = FareCommissionSplit.Validate(
            op.TricycleCommissionPercent,
            request.Tricycle.OperatorCommissionPercent,
            request.Tricycle.DriverCommissionPercent);
        if (tricycleError is not null)
        {
            return BadRequest(new { message = $"Tricycle: {tricycleError}" });
        }

        var motorcycle = await EnsureMatrixAsync(op!.Id, VehicleType.Motorcycle, op.MotorcycleCommissionPercent, cancellationToken);
        var tricycle = await EnsureMatrixAsync(op.Id, VehicleType.Tricycle, op.TricycleCommissionPercent, cancellationToken);
        ApplyRates(motorcycle!, request.Motorcycle);
        ApplyRates(tricycle!, request.Tricycle);
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpPost("surcharges")]
    public async Task<ActionResult<OperatorFareDetailResponse>> AddRelatedSurcharge(
        [FromBody] SaveRelatedFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var types = (request.VehicleTypes ?? [])
            .Where(x => x is VehicleType.Motorcycle or VehicleType.Tricycle)
            .Distinct()
            .ToList();
        if (types.Count == 0)
        {
            types.Add(VehicleType.Motorcycle);
            types.Add(VehicleType.Tricycle);
        }

        var parsed = ParseSurcharge(new SaveFareSurchargeRequest(
            request.Kind,
            request.Name,
            request.Amount,
            request.WindowStart,
            request.WindowEnd,
            request.RangeStartUtc,
            request.RangeEndUtc,
            request.IsActive));
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        foreach (var vehicleType in types)
        {
            var fare = await EnsureMatrixAsync(op!.Id, vehicleType, FareCommissionSplit.SystemPercent(op, vehicleType), cancellationToken);
            fare!.Surcharges.Add(CloneSurcharge(parsed.Item!));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpPost("{vehicleType}/surcharges")]
    public async Task<ActionResult<OperatorFareDetailResponse>> AddSurcharge(
        VehicleType vehicleType,
        [FromBody] SaveFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var fare = await EnsureMatrixAsync(op!.Id, vehicleType, FareCommissionSplit.SystemPercent(op, vehicleType), cancellationToken);
        if (fare is null)
        {
            return BadRequest(new { message = "Choose Motorcycle or Tricycle." });
        }

        var parsed = ParseSurcharge(request);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        fare.Surcharges.Add(parsed.Item!);
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpPut("surcharges/{id:guid}")]
    public async Task<ActionResult<OperatorFareDetailResponse>> UpdateSurcharge(
        Guid id,
        [FromBody] SaveFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.FareSurcharges
            .Include(x => x.FareMatrix)
            .FirstOrDefaultAsync(x => x.Id == id && x.FareMatrix.OperatorId == op!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        var parsed = ParseSurcharge(request);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        row.Kind = parsed.Item!.Kind;
        row.Name = parsed.Item.Name;
        row.Amount = parsed.Item.Amount;
        row.WindowStart = parsed.Item.WindowStart;
        row.WindowEnd = parsed.Item.WindowEnd;
        row.RangeStartUtc = parsed.Item.RangeStartUtc;
        row.RangeEndUtc = parsed.Item.RangeEndUtc;
        row.IsActive = parsed.Item.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    [HttpPost("surcharges/{id:guid}/delete")]
    public async Task<ActionResult<OperatorFareDetailResponse>> DeleteSurcharge(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.FareSurcharges
            .Include(x => x.FareMatrix)
            .FirstOrDefaultAsync(x => x.Id == id && x.FareMatrix.OperatorId == op!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        db.FareSurcharges.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return await Get(cancellationToken);
    }

    private static bool InvalidRates(FareVehicleRatesBody rates)
    {
        if (rates.BaseFare < 0 || rates.PerKm < 0 || rates.MinimumFare < 0 || rates.IncludedKm < 0)
        {
            return true;
        }

        return InvalidTiers(rates.PassengerTiers);
    }

    private static bool InvalidTiers(IReadOnlyList<FarePassengerTierBody>? tiers)
    {
        if (tiers is null || tiers.Count == 0)
        {
            return false;
        }

        if (tiers.Any(x => x.PassengerCount < 1 || x.BaseFare < 0 || x.PerKm < 0 || x.MinimumFare < 0 || x.IncludedKm < 0))
        {
            return true;
        }

        return tiers.Select(x => x.PassengerCount).Distinct().Count() != tiers.Count;
    }

    private static void ApplyRates(FareMatrix fare, FareVehicleRatesBody rates) =>
        ApplyRates(
            fare,
            rates.BaseFare,
            rates.PerKm,
            rates.MinimumFare,
            rates.IncludedKm,
            rates.OperatorCommissionPercent,
            rates.DriverCommissionPercent,
            rates.IsActive,
            rates.PassengerTiers);

    private static void ApplyRates(FareMatrix fare, SaveFareRatesRequest request) =>
        ApplyRates(
            fare,
            request.BaseFare,
            request.PerKm,
            request.MinimumFare,
            request.IncludedKm,
            request.OperatorCommissionPercent,
            request.DriverCommissionPercent,
            request.IsActive,
            request.PassengerTiers);

    private static void ApplyRates(
        FareMatrix fare,
        decimal baseFare,
        decimal perKm,
        decimal minimumFare,
        decimal includedKm,
        decimal operatorCommissionPercent,
        decimal driverCommissionPercent,
        bool isActive,
        IReadOnlyList<FarePassengerTierBody>? passengerTiers)
    {
        var tiers = NormalizeTiers(passengerTiers, baseFare, perKm, minimumFare, includedKm);
        var primary = tiers.OrderBy(x => x.PassengerCount).First();
        fare.BaseFare = FareCommissionSplit.Round(primary.BaseFare);
        fare.PerKm = FareCommissionSplit.Round(primary.PerKm);
        fare.MinimumFare = FareCommissionSplit.Round(primary.MinimumFare);
        fare.IncludedKm = FareCommissionSplit.Round(primary.IncludedKm);
        fare.OperatorCommissionPercent = FareCommissionSplit.Round(operatorCommissionPercent);
        fare.DriverCommissionPercent = FareCommissionSplit.Round(driverCommissionPercent);
        fare.IsActive = isActive;
        fare.UpdatedAtUtc = DateTime.UtcNow;
        ReplacePassengerTiers(fare, tiers);
    }

    private static IReadOnlyList<FarePassengerTierBody> NormalizeTiers(
        IReadOnlyList<FarePassengerTierBody>? passengerTiers,
        decimal baseFare,
        decimal perKm,
        decimal minimumFare,
        decimal includedKm)
    {
        if (passengerTiers is { Count: > 0 })
        {
            return passengerTiers
                .Select(x => new FarePassengerTierBody(
                    Math.Max(1, x.PassengerCount),
                    FareCommissionSplit.Round(x.BaseFare),
                    FareCommissionSplit.Round(x.PerKm),
                    FareCommissionSplit.Round(x.MinimumFare),
                    FareCommissionSplit.Round(x.IncludedKm)))
                .GroupBy(x => x.PassengerCount)
                .Select(g => g.First())
                .OrderBy(x => x.PassengerCount)
                .ToList();
        }

        return
        [
            new FarePassengerTierBody(1, baseFare, perKm, minimumFare, includedKm)
        ];
    }

    private static void ReplacePassengerTiers(FareMatrix fare, IReadOnlyList<FarePassengerTierBody> tiers)
    {
        fare.PassengerTiers.Clear();
        foreach (var tier in tiers)
        {
            fare.PassengerTiers.Add(new FarePassengerTier
            {
                PassengerCount = tier.PassengerCount,
                BaseFare = tier.BaseFare,
                PerKm = tier.PerKm,
                MinimumFare = tier.MinimumFare,
                IncludedKm = tier.IncludedKm
            });
        }
    }

    private async Task<FareMatrix?> EnsureMatrixAsync(
        Guid operatorId,
        VehicleType vehicleType,
        decimal systemPercent,
        CancellationToken cancellationToken)
    {
        if (vehicleType is not VehicleType.Motorcycle and not VehicleType.Tricycle)
        {
            return null;
        }

        var fare = await db.FareMatrices
            .Include(x => x.Surcharges)
            .Include(x => x.PassengerTiers)
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.VehicleType == vehicleType, cancellationToken);
        if (fare is not null)
        {
            return fare;
        }

        fare = new FareMatrix { OperatorId = operatorId, VehicleType = vehicleType, IncludedKm = 1, IsActive = true };
        FareCommissionSplit.ApplyDefaults(fare, systemPercent);
        db.FareMatrices.Add(fare);
        return fare;
    }

    private static FareSurcharge CloneSurcharge(FareSurcharge source) =>
        new()
        {
            Kind = source.Kind,
            Name = source.Name,
            Amount = source.Amount,
            WindowStart = source.WindowStart,
            WindowEnd = source.WindowEnd,
            RangeStartUtc = source.RangeStartUtc,
            RangeEndUtc = source.RangeEndUtc,
            IsActive = source.IsActive
        };

    private static (FareSurcharge? Item, string? Error) ParseSurcharge(SaveFareSurchargeRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || request.Amount < 0)
        {
            return (null, "Surcharge name and a non-negative amount are required.");
        }

        var item = new FareSurcharge
        {
            Kind = request.Kind,
            Name = name,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            IsActive = request.IsActive
        };

        if (request.Kind == SurchargeKind.TimeWindow)
        {
            if (!TimeOnly.TryParse(request.WindowStart, out var start) || !TimeOnly.TryParse(request.WindowEnd, out var end))
            {
                return (null, "Time-window surcharges need a start and end time in Philippine time.");
            }

            item.WindowStart = start;
            item.WindowEnd = end;
            return (item, null);
        }

        if (request.Kind != SurchargeKind.DateRange)
        {
            return (null, "Choose a time window or a date range.");
        }

        if (request.RangeStartUtc is null || request.RangeEndUtc is null)
        {
            return (null, "Date-range surcharges need a start and end.");
        }

        var from = DateTime.SpecifyKind(request.RangeStartUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.RangeEndUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        item.RangeStartUtc = from;
        item.RangeEndUtc = to;
        return (item, null);
    }
}
