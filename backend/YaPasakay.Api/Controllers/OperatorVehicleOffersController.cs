using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/vehicle-offers")]
public class OperatorVehicleOffersController(AppDbContext db) : ControllerBase
{
    public record VehicleOfferDto(
        Guid Id,
        Guid VehicleCategoryId,
        string Code,
        string Name,
        string IconKey,
        int MaxPassengers,
        bool IsCargo,
        bool IsEnabled,
        decimal CommissionPercent,
        string? DisplayName,
        int? MaxPassengersOverride,
        bool IsCustom,
        string VehicleType);

    public record SaveOfferRequest(
        bool IsEnabled,
        decimal CommissionPercent,
        string? DisplayName,
        int? MaxPassengers);

    public record CreateCustomCategoryRequest(
        string Name,
        string? Code,
        int MaxPassengers,
        string IconKey,
        bool IsCargo,
        decimal CommissionPercent,
        bool IsEnabled);

    public record UpdateCustomCategoryRequest(
        string Name,
        int MaxPassengers,
        string IconKey,
        bool IsCargo);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleOfferDto>>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null) return StatusCode(status, new { message });

        await VehicleCatalogBootstrap.EnsureAsync(db, cancellationToken);

        var rows = await db.OperatorVehicleOffers
            .AsNoTracking()
            .Include(x => x.VehicleCategory)
            .Where(x => x.OperatorId == op.Id && x.VehicleCategory != null && x.VehicleCategory.IsActive)
            .OrderBy(x => x.VehicleCategory!.SortOrder)
            .ThenBy(x => x.VehicleCategory!.Name)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(Map).ToList());
    }

    [HttpPut("{offerId:guid}")]
    public async Task<ActionResult<VehicleOfferDto>> Update(Guid offerId, [FromBody] SaveOfferRequest request, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null) return StatusCode(status, new { message });

        var offer = await db.OperatorVehicleOffers
            .Include(x => x.VehicleCategory)
            .FirstOrDefaultAsync(x => x.Id == offerId && x.OperatorId == op.Id, cancellationToken);
        if (offer?.VehicleCategory is null) return NotFound(new { message = "Vehicle offer not found." });

        if (request.CommissionPercent < 0 || request.CommissionPercent > 100)
        {
            return BadRequest(new { message = "Commission must be between 0 and 100." });
        }

        if (request.MaxPassengers is int max && max < 1)
        {
            return BadRequest(new { message = "Max passengers must be at least 1." });
        }

        // Platform commission is admin-owned; operators only toggle enable / display / capacity.
        offer.IsEnabled = request.IsEnabled;
        offer.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        offer.MaxPassengers = request.MaxPassengers;
        offer.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(offer));
    }

    [HttpPost("custom")]
    public Task<ActionResult<VehicleOfferDto>> CreateCustom([FromBody] CreateCustomCategoryRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult<ActionResult<VehicleOfferDto>>(StatusCode(
            StatusCodes.Status403Forbidden,
            new { message = "Only Super Admin can create vehicle types. Ask admin to set commissions and catalog types." }));
    }

    [HttpPut("custom/{categoryId:guid}")]
    public Task<ActionResult<VehicleOfferDto>> UpdateCustomCategory(
        Guid categoryId,
        [FromBody] UpdateCustomCategoryRequest request,
        CancellationToken cancellationToken)
    {
        _ = categoryId;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult<ActionResult<VehicleOfferDto>>(StatusCode(
            StatusCodes.Status403Forbidden,
            new { message = "Only Super Admin can edit custom vehicle types." }));
    }

    [HttpPost("custom/{categoryId:guid}/deactivate")]
    public Task<ActionResult<VehicleOfferDto>> DeactivateCustomCategory(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        _ = categoryId;
        _ = cancellationToken;
        return Task.FromResult<ActionResult<VehicleOfferDto>>(StatusCode(
            StatusCodes.Status403Forbidden,
            new { message = "Only Super Admin can deactivate custom vehicle types." }));
    }

    private static VehicleOfferDto Map(OperatorVehicleOffer offer)
    {
        var cat = offer.VehicleCategory!;
        var legacy = cat.LegacyEnumValue is int v && Enum.IsDefined(typeof(Domain.Enums.VehicleType), v)
            ? ((Domain.Enums.VehicleType)v).ToString()
            : cat.Code;
        return new VehicleOfferDto(
            offer.Id,
            cat.Id,
            cat.Code,
            offer.DisplayName ?? cat.Name,
            cat.IconKey,
            offer.MaxPassengers ?? cat.MaxPassengers,
            cat.IsCargo,
            offer.IsEnabled,
            offer.CommissionPercent,
            offer.DisplayName,
            offer.MaxPassengers,
            cat.OperatorId is not null,
            legacy);
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
