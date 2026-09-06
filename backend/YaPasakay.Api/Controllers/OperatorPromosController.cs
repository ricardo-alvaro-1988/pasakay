using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/promos")]
public class OperatorPromosController(AppDbContext db, OperatorPromoService promos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorPromoListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.OperatorPromos
            .Where(x => x.OperatorId == op.Id)
            .OrderByDescending(x => x.DiscountPercent)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return Ok(new OperatorPromoListResponse(rows.Select(Map).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<OperatorPromoItem>> Create(
        [FromBody] SaveOperatorPromoRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var code = OperatorPromoRules.CodeForPercent(request.DiscountPercent);
        if (await db.OperatorPromos.AnyAsync(x => x.OperatorId == op.Id && x.Code == code, cancellationToken))
        {
            return BadRequest(new { message = $"Promo {OperatorPromoRules.DisplayCode(request.DiscountPercent)} already exists. Edit it instead." });
        }

        var row = new OperatorPromo
        {
            OperatorId = op.Id,
            Code = code,
            DiscountPercent = request.DiscountPercent,
            IsActive = request.IsActive,
            StartsAtUtc = Normalize(request.StartsAtUtc),
            EndsAtUtc = Normalize(request.EndsAtUtc),
            MaxRedemptions = request.MaxRedemptions is int n && n > 0 ? n : null,
        };
        db.OperatorPromos.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperatorPromoItem>> Update(
        Guid id,
        [FromBody] SaveOperatorPromoRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var row = await db.OperatorPromos.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Promo not found." });
        }

        var code = OperatorPromoRules.CodeForPercent(request.DiscountPercent);
        if (await db.OperatorPromos.AnyAsync(
                x => x.OperatorId == op.Id && x.Code == code && x.Id != id,
                cancellationToken))
        {
            return BadRequest(new { message = $"Promo {OperatorPromoRules.DisplayCode(request.DiscountPercent)} already exists." });
        }

        row.DiscountPercent = request.DiscountPercent;
        row.Code = code;
        row.IsActive = request.IsActive;
        row.StartsAtUtc = Normalize(request.StartsAtUtc);
        row.EndsAtUtc = Normalize(request.EndsAtUtc);
        row.MaxRedemptions = request.MaxRedemptions is int n && n > 0 ? n : null;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<OperatorPromoItem>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPromos.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Promo not found." });
        }

        row.IsActive = !row.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    static string? Validate(SaveOperatorPromoRequest request)
    {
        if (request.DiscountPercent is < 1 or > 100)
        {
            return "Discount percent must be between 1 and 100.";
        }

        if (request.StartsAtUtc is DateTime start && request.EndsAtUtc is DateTime end && end < start)
        {
            return "End date must be after start date.";
        }

        return null;
    }

    static DateTime? Normalize(DateTime? value) =>
        value is DateTime at ? DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc) : null;

    static OperatorPromoItem Map(OperatorPromo row) =>
        new(
            row.Id,
            row.Code,
            OperatorPromoRules.DisplayCode(row.DiscountPercent),
            row.DiscountPercent,
            row.IsActive,
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.MaxRedemptions,
            row.RedemptionCount,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
}
