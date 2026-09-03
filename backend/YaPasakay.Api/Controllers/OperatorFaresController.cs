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
    public async Task<ActionResult<OperatorFareDetailResponse>> Get(
        [FromQuery] Guid? municipalityId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await BuildDetailAsync(op!, municipalityId, cancellationToken));
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

        var coverageError = await RequireCoveredMunicipalityAsync(op!.Id, request.MunicipalityId, cancellationToken);
        if (coverageError is not null)
        {
            return BadRequest(new { message = coverageError });
        }

        var splitError = FareCommissionSplit.Validate(
            FareCommissionSplit.SystemPercent(op, request.VehicleType),
            request.OperatorCommissionPercent,
            request.DriverCommissionPercent);
        if (splitError is not null)
        {
            return BadRequest(new { message = splitError });
        }

        var fare = await EnsureMatrixAsync(
            op.Id,
            request.MunicipalityId,
            request.VehicleType,
            FareCommissionSplit.SystemPercent(op, request.VehicleType),
            cancellationToken);
        try
        {
            var rates = request.VehicleType == VehicleType.Motorcycle
                ? SinglePassengerRates(ToVehicleBody(request))
                : ToVehicleBody(request);
            await PersistRatesAsync(fare!, rates, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = DescribeDbError(ex),
            });
        }

        return Ok(await BuildDetailAsync(op, request.MunicipalityId, cancellationToken));
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

        var coverageError = await RequireCoveredMunicipalityAsync(op!.Id, request.MunicipalityId, cancellationToken);
        if (coverageError is not null)
        {
            return BadRequest(new { message = coverageError });
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

        var motorcycle = await EnsureMatrixAsync(
            op.Id,
            request.MunicipalityId,
            VehicleType.Motorcycle,
            op.MotorcycleCommissionPercent,
            cancellationToken);
        var tricycle = await EnsureMatrixAsync(
            op.Id,
            request.MunicipalityId,
            VehicleType.Tricycle,
            op.TricycleCommissionPercent,
            cancellationToken);
        try
        {
            await PersistRatesAsync(motorcycle!, SinglePassengerRates(request.Motorcycle), cancellationToken);
            await PersistRatesAsync(tricycle!, request.Tricycle, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = DescribeDbError(ex),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = $"Could not save fare matrix. {ex.GetBaseException().Message}",
            });
        }

        return Ok(await BuildDetailAsync(op, request.MunicipalityId, cancellationToken));
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

        var coverageError = await RequireCoveredMunicipalityAsync(op!.Id, request.MunicipalityId, cancellationToken);
        if (coverageError is not null)
        {
            return BadRequest(new { message = coverageError });
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
            var fare = await EnsureMatrixAsync(
                op.Id,
                request.MunicipalityId,
                vehicleType,
                FareCommissionSplit.SystemPercent(op, vehicleType),
                cancellationToken);
            fare!.Surcharges.Add(CloneSurcharge(parsed.Item!));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildDetailAsync(op, request.MunicipalityId, cancellationToken));
    }

    [HttpPost("{vehicleType}/surcharges")]
    public async Task<ActionResult<OperatorFareDetailResponse>> AddSurcharge(
        VehicleType vehicleType,
        [FromQuery] Guid municipalityId,
        [FromBody] SaveFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var coverageError = await RequireCoveredMunicipalityAsync(op!.Id, municipalityId, cancellationToken);
        if (coverageError is not null)
        {
            return BadRequest(new { message = coverageError });
        }

        var fare = await EnsureMatrixAsync(
            op.Id,
            municipalityId,
            vehicleType,
            FareCommissionSplit.SystemPercent(op, vehicleType),
            cancellationToken);
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
        return Ok(await BuildDetailAsync(op, municipalityId, cancellationToken));
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
        return Ok(await BuildDetailAsync(op, row.FareMatrix.MunicipalityId, cancellationToken));
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

        var municipalityId = row.FareMatrix.MunicipalityId;
        db.FareSurcharges.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildDetailAsync(op, municipalityId, cancellationToken));
    }

    private async Task<OperatorFareDetailResponse> BuildDetailAsync(
        Operator op,
        Guid? municipalityId,
        CancellationToken cancellationToken)
    {
        var municipalities = await OperatorMaps.OperatorMunicipalitiesAsync(db, op.Id, cancellationToken);
        var selectedId = municipalityId
            ?? municipalities.FirstOrDefault()?.Id
            ?? await db.FareMatrices
                .Where(x => x.OperatorId == op.Id)
                .OrderBy(x => x.Municipality.Name)
                .Select(x => (Guid?)x.MunicipalityId)
                .FirstOrDefaultAsync(cancellationToken);

        var fares = selectedId is null
            ? []
            : await db.FareMatrices
                .Include(x => x.Municipality)
                .Include(x => x.Surcharges)
                .Include(x => x.PassengerTiers)
                .Where(x => x.OperatorId == op.Id && x.MunicipalityId == selectedId)
                .ToListAsync(cancellationToken);

        var selectedName = selectedId is null
            ? null
            : municipalities.FirstOrDefault(x => x.Id == selectedId)?.Name
                ?? fares.FirstOrDefault()?.Municipality.Name
                ?? await db.Municipalities
                    .Where(x => x.Id == selectedId)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken);

        return new OperatorFareDetailResponse(
            op.Id,
            op.CompanyName,
            op.IsActive,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            selectedId,
            selectedName,
            municipalities,
            OperatorMaps.FareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Motorcycle), true),
            OperatorMaps.FareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle), true));
    }

    private async Task<string?> RequireCoveredMunicipalityAsync(
        Guid operatorId,
        Guid municipalityId,
        CancellationToken cancellationToken)
    {
        var covered = await db.OperatorBarangays
            .AnyAsync(x => x.OperatorId == operatorId && x.Barangay.MunicipalityId == municipalityId, cancellationToken);
        if (!covered)
        {
            return "Choose a municipality in your service area.";
        }

        return null;
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

    private static FareVehicleRatesBody SinglePassengerRates(FareVehicleRatesBody rates)
    {
        var primary = rates.PassengerTiers?
            .OrderBy(x => x.PassengerCount == 1 ? 0 : 1)
            .ThenBy(x => x.PassengerCount)
            .FirstOrDefault();
        var baseFare = primary?.BaseFare ?? rates.BaseFare;
        var perKm = primary?.PerKm ?? rates.PerKm;
        var minimumFare = primary?.MinimumFare ?? rates.MinimumFare;
        var includedKm = primary?.IncludedKm ?? rates.IncludedKm;
        return rates with
        {
            BaseFare = baseFare,
            PerKm = perKm,
            MinimumFare = minimumFare,
            IncludedKm = includedKm,
            PassengerTiers =
            [
                new FarePassengerTierBody(1, baseFare, perKm, minimumFare, includedKm)
            ]
        };
    }

    private static FareVehicleRatesBody ToVehicleBody(SaveFareRatesRequest request) =>
        new(
            request.BaseFare,
            request.PerKm,
            request.MinimumFare,
            request.IncludedKm,
            request.OperatorCommissionPercent,
            request.DriverCommissionPercent,
            request.IsActive,
            request.PassengerTiers);

    private static string DescribeDbError(DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return "Fare rates changed while saving. Refresh the page and try again.";
        }

        var root = ex.InnerException?.Message ?? ex.Message;
        if (root.Contains("IX_FarePassengerTiers_FareMatrixId_PassengerCount", StringComparison.OrdinalIgnoreCase)
            || root.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || root.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not update passenger tiers because of a duplicate person count. Refresh and try again.";
        }

        if (root.Contains("Municipality", StringComparison.OrdinalIgnoreCase)
            || root.Contains("FK_FareMatrices_Municipalities", StringComparison.OrdinalIgnoreCase))
        {
            return "That municipality is missing in the database. Re-select the municipality and try again.";
        }

        return $"Could not save fare matrix. {root}";
    }

    private async Task PersistRatesAsync(
        FareMatrix fare,
        FareVehicleRatesBody rates,
        CancellationToken cancellationToken)
    {
        var tiers = NormalizeTiers(
            rates.PassengerTiers,
            rates.BaseFare,
            rates.PerKm,
            rates.MinimumFare,
            rates.IncludedKm);
        var primary = tiers.OrderBy(x => x.PassengerCount).First();
        fare.BaseFare = FareCommissionSplit.Round(primary.BaseFare);
        fare.PerKm = FareCommissionSplit.Round(primary.PerKm);
        fare.MinimumFare = FareCommissionSplit.Round(primary.MinimumFare);
        fare.IncludedKm = FareCommissionSplit.Round(primary.IncludedKm);
        fare.OperatorCommissionPercent = FareCommissionSplit.Round(rates.OperatorCommissionPercent);
        fare.DriverCommissionPercent = FareCommissionSplit.Round(rates.DriverCommissionPercent);
        fare.IsActive = rates.IsActive;
        fare.UpdatedAtUtc = DateTime.UtcNow;

        // 1. Save FareMatrix scalar changes (and insert new matrix row when State == Added).
        //    Do NOT touch PassengerTiers through EF — we manage them via raw SQL below
        //    to avoid unique-index conflicts from the change-tracker.
        var isNew = db.Entry(fare).State == EntityState.Added;
        DetachPassengerTiers(fare);
        await db.SaveChangesAsync(cancellationToken);

        // 2. Delete all existing tiers directly (no-op on insert).
        if (!isNew)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM FarePassengerTiers WHERE FareMatrixId = {0}",
                [fare.Id],
                cancellationToken);
        }

        // 3. Insert new tiers directly.
        var now = DateTime.UtcNow;
        foreach (var tier in tiers)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO FarePassengerTiers (Id, FareMatrixId, PassengerCount, BaseFare, PerKm, MinimumFare, IncludedKm, CreatedAtUtc) VALUES ({0},{1},{2},{3},{4},{5},{6},{7})",
                [Guid.NewGuid(), fare.Id, tier.PassengerCount, tier.BaseFare, tier.PerKm, tier.MinimumFare, tier.IncludedKm, now],
                cancellationToken);
        }
    }

    private void DetachPassengerTiers(FareMatrix fare)
    {
        foreach (var entry in db.ChangeTracker.Entries<FarePassengerTier>()
            .Where(e => e.Entity.FareMatrixId == fare.Id || ReferenceEquals(e.Entity.FareMatrix, fare))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        fare.PassengerTiers.Clear();
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

    private async Task<FareMatrix?> EnsureMatrixAsync(
        Guid operatorId,
        Guid municipalityId,
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
            .FirstOrDefaultAsync(
                x => x.OperatorId == operatorId && x.VehicleType == vehicleType && x.MunicipalityId == municipalityId,
                cancellationToken);
        if (fare is not null)
        {
            return fare;
        }

        fare = new FareMatrix
        {
            OperatorId = operatorId,
            MunicipalityId = municipalityId,
            VehicleType = vehicleType,
            IncludedKm = 1,
            IsActive = true
        };
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
