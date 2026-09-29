using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain;
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

    [HttpGet("offering-terms")]
    public ActionResult<VehicleOfferingTermsInfo> OfferingTerms() =>
        Ok(new VehicleOfferingTermsInfo(VehicleOfferingTerms.Version, VehicleOfferingTerms.Text));

    [HttpGet("offering-logs")]
    public async Task<ActionResult<PagedResult<VehicleOfferingLogItem>>> OfferingLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null) return StatusCode(status, new { message });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.VehicleOfferingLogs.AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderByDescending(x => x.AtUtc);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VehicleOfferingLogItem(
                x.Id,
                x.OperatorId,
                op.CompanyName,
                x.MunicipalityId,
                x.Municipality != null ? x.Municipality.Name : null,
                x.VehicleCategoryId,
                x.VehicleCode,
                x.VehicleName,
                x.VehicleType.ToString(),
                x.IsOffered,
                x.ActorName,
                x.ActorRole,
                x.AcceptedTerms,
                x.TermsVersion,
                x.AtUtc))
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<VehicleOfferingLogItem>(rows, page, pageSize, total));
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

        if (VehicleTypeRules.ValidateChoice(request.VehicleType) is { } invalidVehicle)
        {
            return BadRequest(new { message = invalidVehicle });
        }

        if (request.VehicleType == VehicleType.Custom && request.VehicleCategoryId is null)
        {
            return BadRequest(new { message = "Custom fares require a vehicle category." });
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

        // Include offers for commission lookup + enable gate
        await db.Entry(op).Collection(x => x.VehicleOffers).LoadAsync(cancellationToken);

        var categoryId = request.VehicleCategoryId ?? VehicleCatalog.IdFor(request.VehicleType);
        var offer = op.VehicleOffers.FirstOrDefault(x => x.VehicleCategoryId == categoryId);
        if (offer is null || !offer.IsEnabled)
        {
            return BadRequest(new { message = "That vehicle type is not enabled for this operator. Ask Super Admin to enable it." });
        }

        var systemPercent = FareCommissionSplit.SystemPercent(op, request.VehicleCategoryId, request.VehicleType);
        var splitError = FareCommissionSplit.Validate(
            systemPercent,
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
            request.VehicleCategoryId,
            systemPercent,
            cancellationToken);
        try
        {
            var rates = VehicleTypeRules.UsesSinglePassengerTier(request.VehicleType)
                ? SinglePassengerRates(ToVehicleBody(request))
                : ToVehicleBody(request);
            var offerError = await PersistRatesAsync(fare!, rates, User, cancellationToken);
            if (offerError is not null)
            {
                return BadRequest(new { message = offerError });
            }
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

        await db.Entry(op).Collection(x => x.VehicleOffers).LoadAsync(cancellationToken);
        var motorcycleEnabled = op.VehicleOffers.Any(x =>
            x.IsEnabled && x.VehicleCategoryId == VehicleCatalog.IdFor(VehicleType.Motorcycle));
        var tricycleEnabled = op.VehicleOffers.Any(x =>
            x.IsEnabled && x.VehicleCategoryId == VehicleCatalog.IdFor(VehicleType.Tricycle));
        if (!motorcycleEnabled && !tricycleEnabled)
        {
            return BadRequest(new { message = "Motorcycle and Tricycle are not enabled for this operator." });
        }

        if (motorcycleEnabled)
        {
            var motorcycleError = FareCommissionSplit.Validate(
                op.MotorcycleCommissionPercent,
                request.Motorcycle.OperatorCommissionPercent,
                request.Motorcycle.DriverCommissionPercent);
            if (motorcycleError is not null)
            {
                return BadRequest(new { message = $"Motorcycle: {motorcycleError}" });
            }
        }

        if (tricycleEnabled)
        {
            var tricycleError = FareCommissionSplit.Validate(
                op.TricycleCommissionPercent,
                request.Tricycle.OperatorCommissionPercent,
                request.Tricycle.DriverCommissionPercent);
            if (tricycleError is not null)
            {
                return BadRequest(new { message = $"Tricycle: {tricycleError}" });
            }
        }

        try
        {
            if (motorcycleEnabled)
            {
                var motorcycle = await EnsureMatrixAsync(
                    op.Id,
                    request.MunicipalityId,
                    VehicleType.Motorcycle,
                    null,
                    op.MotorcycleCommissionPercent,
                    cancellationToken);
                var mcError = await PersistRatesAsync(motorcycle!, SinglePassengerRates(request.Motorcycle), User, cancellationToken);
                if (mcError is not null) return BadRequest(new { message = mcError });
            }

            if (tricycleEnabled)
            {
                var tricycle = await EnsureMatrixAsync(
                    op.Id,
                    request.MunicipalityId,
                    VehicleType.Tricycle,
                    null,
                    op.TricycleCommissionPercent,
                    cancellationToken);
                var trikeError = await PersistRatesAsync(tricycle!, request.Tricycle, User, cancellationToken);
                if (trikeError is not null) return BadRequest(new { message = trikeError });
            }
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

        var municipalityIds = (request.MunicipalityIds ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        if (municipalityIds.Count == 0 && request.MunicipalityId != Guid.Empty)
        {
            municipalityIds.Add(request.MunicipalityId);
        }

        if (municipalityIds.Count == 0)
        {
            return BadRequest(new { message = "Choose at least one municipality." });
        }

        foreach (var municipalityId in municipalityIds)
        {
            var coverageError = await RequireCoveredMunicipalityAsync(op!.Id, municipalityId, cancellationToken);
            if (coverageError is not null)
            {
                return BadRequest(new { message = coverageError });
            }
        }

        var types = (request.VehicleTypes ?? [])
            .Where(VehicleTypeRules.IsKnown)
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

        try
        {
            foreach (var municipalityId in municipalityIds)
            {
                foreach (var vehicleType in types)
                {
                    var fare = await EnsureMatrixAsync(
                        op.Id,
                        municipalityId,
                        vehicleType,
                        null,
                        FareCommissionSplit.SystemPercent(op, vehicleType),
                        cancellationToken);
                    await PersistSurchargeAsync(fare!, parsed.Item!, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = DescribeSurchargeDbError(ex),
            });
        }

        var viewMunicipalityId = request.MunicipalityId != Guid.Empty
            && municipalityIds.Contains(request.MunicipalityId)
                ? request.MunicipalityId
                : municipalityIds[0];
        return Ok(await BuildDetailAsync(op, viewMunicipalityId, cancellationToken));
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
            null,
            FareCommissionSplit.SystemPercent(op, vehicleType),
            cancellationToken);
        if (fare is null)
        {
            return BadRequest(new { message = "Choose a valid vehicle type." });
        }

        var parsed = ParseSurcharge(request);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        try
        {
            await PersistSurchargeAsync(fare, parsed.Item!, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = DescribeSurchargeDbError(ex),
            });
        }

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

        await db.Entry(op).Collection(x => x.VehicleOffers)
            .Query()
            .Include(o => o.VehicleCategory)
            .LoadAsync(cancellationToken);

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
            OperatorMaps.FareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle), true),
            OperatorMaps.VehicleFareSlots(op, fares, includeSamples: true));
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
            request.PassengerTiers,
            request.AcceptedOfferTerms);

    private static string DescribeSurchargeDbError(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return "Could not save surcharge because fare data changed. Refresh the page and try again.";
        }

        if (ex is DbUpdateException dbEx)
        {
            return $"Could not save surcharge. {dbEx.GetBaseException().Message}";
        }

        return $"Could not save surcharge. {ex.GetBaseException().Message}";
    }

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

    private async Task<string?> PersistRatesAsync(
        FareMatrix fare,
        FareVehicleRatesBody rates,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var previousOffered = fare.IsActive;
        var nextOffered = rates.IsActive;
        if (nextOffered
            && !previousOffered
            && VehicleOfferingTerms.RequiresTermsAcceptance(fare.VehicleType)
            && !rates.AcceptedOfferTerms)
        {
            return "Accept the vehicle offering terms and conditions before offering this vehicle type.";
        }

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
        fare.IsActive = nextOffered;
        fare.UpdatedAtUtc = DateTime.UtcNow;

        if (previousOffered != nextOffered)
        {
            await AddOfferingLogAsync(fare, nextOffered, rates.AcceptedOfferTerms, user, cancellationToken);
        }

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

        // Keep vehicle offer seat capacity aligned with the highest passenger fare tier.
        await SyncOfferMaxPassengersAsync(fare.OperatorId, fare.VehicleType, fare.VehicleCategoryId, tiers, cancellationToken);

        return null;
    }

    private async Task SyncOfferMaxPassengersAsync(
        Guid operatorId,
        VehicleType vehicleType,
        Guid? vehicleCategoryId,
        IReadOnlyList<FarePassengerTierBody> tiers,
        CancellationToken cancellationToken)
    {
        if (VehicleTypeRules.UsesSinglePassengerTier(vehicleType) || VehicleTypeRules.IsCargo(vehicleType))
        {
            return;
        }

        var categoryId = vehicleCategoryId ?? VehicleCatalog.IdFor(vehicleType);
        var offer = await db.OperatorVehicleOffers
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.VehicleCategoryId == categoryId, cancellationToken);
        if (offer is null)
        {
            return;
        }

        var maxSeats = tiers.Count > 0 ? tiers.Max(t => t.PassengerCount) : VehicleTypeRules.MaxPassengers(vehicleType);
        if (offer.MaxPassengers == maxSeats)
        {
            return;
        }

        offer.MaxPassengers = Math.Max(1, maxSeats);
        offer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddOfferingLogAsync(
        FareMatrix fare,
        bool isOffered,
        bool acceptedTerms,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid? actorId = null;
        var raw = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(raw, out var id))
        {
            actorId = id;
        }

        string actorName = user.FindFirst("name")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? user.Identity?.Name
            ?? "Operator";
        if (actorId is Guid uid)
        {
            var named = await db.Users.AsNoTracking()
                .Where(x => x.Id == uid)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(named))
            {
                actorName = named;
            }
        }

        string code;
        string name;
        if (fare.VehicleCategoryId is Guid categoryId)
        {
            var cat = await db.VehicleCategories.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
            code = cat?.Code ?? fare.VehicleType.ToString().ToLowerInvariant();
            name = cat?.Name ?? fare.VehicleType.ToString();
        }
        else
        {
            var preset = VehicleCatalog.PresetFor(fare.VehicleType);
            code = preset?.Code ?? fare.VehicleType.ToString().ToLowerInvariant();
            name = preset?.Name ?? fare.VehicleType.ToString();
        }

        db.VehicleOfferingLogs.Add(new VehicleOfferingLog
        {
            OperatorId = fare.OperatorId,
            MunicipalityId = fare.MunicipalityId,
            VehicleCategoryId = fare.VehicleCategoryId ?? VehicleCatalog.IdFor(fare.VehicleType),
            VehicleType = fare.VehicleType,
            VehicleCode = code,
            VehicleName = name,
            IsOffered = isOffered,
            ActorUserId = actorId,
            ActorName = actorName.Length > 120 ? actorName[..120] : actorName,
            ActorRole = "Operator",
            AcceptedTerms = isOffered && acceptedTerms,
            TermsVersion = isOffered && VehicleOfferingTerms.RequiresTermsAcceptance(fare.VehicleType)
                ? VehicleOfferingTerms.Version
                : null,
            AtUtc = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Insert by FK only — never via FareMatrix.Surcharges — so the change tracker does not
    /// rewrite related PassengerTiers/Surcharges and throw DbUpdateConcurrencyException.
    /// </summary>
    private async Task PersistSurchargeAsync(
        FareMatrix fare,
        FareSurcharge item,
        CancellationToken cancellationToken)
    {
        // Ignore dirty Operator (and other) rows so SaveChanges only inserts what we need.
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (ReferenceEquals(entry.Entity, fare) || entry.Entity is FareSurcharge)
            {
                continue;
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                entry.State = EntityState.Unchanged;
            }
        }

        if (db.Entry(fare).State == EntityState.Added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        db.FareSurcharges.Add(new FareSurcharge
        {
            FareMatrixId = fare.Id,
            Kind = item.Kind,
            Name = item.Name,
            Amount = item.Amount,
            WindowStart = item.WindowStart,
            WindowEnd = item.WindowEnd,
            RangeStartUtc = item.RangeStartUtc,
            RangeEndUtc = item.RangeEndUtc,
            IsActive = item.IsActive,
        });
        await db.SaveChangesAsync(cancellationToken);
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
        Guid? vehicleCategoryId,
        decimal systemPercent,
        CancellationToken cancellationToken)
    {
        if (vehicleCategoryId is Guid categoryId)
        {
            var category = await db.VehicleCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == categoryId && x.IsActive, cancellationToken);
            if (category is null)
            {
                return null;
            }

            if (category.OperatorId is null)
            {
                vehicleType = category.LegacyEnumValue is int legacy && Enum.IsDefined(typeof(VehicleType), legacy)
                    ? (VehicleType)legacy
                    : VehicleCatalog.TypeFor(category.Id) ?? vehicleType;
            }
            else if (category.OperatorId != operatorId)
            {
                return null;
            }
            else
            {
                vehicleType = VehicleType.Custom;
            }

            var byCategory = await db.FareMatrices
                .FirstOrDefaultAsync(
                    x => x.OperatorId == operatorId
                        && x.MunicipalityId == municipalityId
                        && x.VehicleCategoryId == categoryId,
                    cancellationToken);
            if (byCategory is not null)
            {
                return byCategory;
            }

            // Adopt legacy VehicleType-only row instead of creating a duplicate Sedan (etc.) matrix.
            var byType = await db.FareMatrices
                .FirstOrDefaultAsync(
                    x => x.OperatorId == operatorId
                        && x.MunicipalityId == municipalityId
                        && x.VehicleType == vehicleType
                        && x.VehicleCategoryId == null,
                    cancellationToken);
            if (byType is not null)
            {
                byType.VehicleCategoryId = categoryId;
                return byType;
            }

            var created = new FareMatrix
            {
                OperatorId = operatorId,
                MunicipalityId = municipalityId,
                VehicleType = vehicleType,
                VehicleCategoryId = categoryId,
                IncludedKm = 1,
                IsActive = VehicleOfferingTerms.DefaultOffered(vehicleType)
            };
            FareCommissionSplit.ApplyDefaults(created, systemPercent);
            db.FareMatrices.Add(created);
            return created;
        }

        if (!VehicleTypeRules.IsKnown(vehicleType))
        {
            return null;
        }

        // Do not Include PassengerTiers/Surcharges here: those collections are persisted with
        // raw SQL in PersistRatesAsync / PersistSurchargeAsync. Tracking them causes concurrency
        // conflicts when SaveChanges runs for an unrelated insert.
        var fare = await db.FareMatrices
            .FirstOrDefaultAsync(
                x => x.OperatorId == operatorId && x.VehicleType == vehicleType && x.MunicipalityId == municipalityId,
                cancellationToken);
        if (fare is not null)
        {
            if (fare.VehicleCategoryId is null && VehicleTypeRules.IsKnown(vehicleType))
            {
                fare.VehicleCategoryId = VehicleCatalog.IdFor(vehicleType);
            }
            return fare;
        }

        fare = new FareMatrix
        {
            OperatorId = operatorId,
            MunicipalityId = municipalityId,
            VehicleType = vehicleType,
            VehicleCategoryId = VehicleCatalog.IdFor(vehicleType),
            IncludedKm = 1,
            IsActive = VehicleOfferingTerms.DefaultOffered(vehicleType)
        };
        FareCommissionSplit.ApplyDefaults(fare, systemPercent);
        db.FareMatrices.Add(fare);
        return fare;
    }

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
            Name = name.Length > 80 ? name[..80] : name,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            IsActive = request.IsActive
        };

        if (request.Kind == SurchargeKind.TimeWindow)
        {
            if (!TryParseWindowTime(request.WindowStart, out var start)
                || !TryParseWindowTime(request.WindowEnd, out var end))
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

    private static bool TryParseWindowTime(string? value, out TimeOnly time)
    {
        time = default;
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return false;
        }

        // Browsers send HH:mm or HH:mm:ss; always parse as invariant (not server culture).
        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        if (raw.Length == 5
            && TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        return TimeOnly.TryParseExact(raw, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
