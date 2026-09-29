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
[Route("api/operator/pabili-browse-categories")]
public class OperatorPabiliBrowseCategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorPabiliBrowseCategoryListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.OperatorPabiliBrowseCategories
            .AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(new OperatorPabiliBrowseCategoryListResponse(rows.Select(Map).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<OperatorPabiliBrowseCategoryItem>> Create(
        [FromBody] SaveOperatorPabiliBrowseCategoryRequest request,
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

        var name = request.Name.Trim();
        if (await db.OperatorPabiliBrowseCategories.AnyAsync(
                x => x.OperatorId == op.Id && x.Name == name,
                cancellationToken))
        {
            return Conflict(new { message = $"“{name}” already exists." });
        }

        var row = new OperatorPabiliBrowseCategory
        {
            OperatorId = op.Id,
            Name = name,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
        };
        db.OperatorPabiliBrowseCategories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperatorPabiliBrowseCategoryItem>> Update(
        Guid id,
        [FromBody] SaveOperatorPabiliBrowseCategoryRequest request,
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

        var row = await db.OperatorPabiliBrowseCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var name = request.Name.Trim();
        if (await db.OperatorPabiliBrowseCategories.AnyAsync(
                x => x.OperatorId == op.Id && x.Name == name && x.Id != id,
                cancellationToken))
        {
            return Conflict(new { message = $"“{name}” already exists." });
        }

        row.Name = name;
        row.IsActive = request.IsActive;
        row.SortOrder = request.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<OperatorPabiliBrowseCategoryItem>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPabiliBrowseCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        row.IsActive = !row.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPabiliBrowseCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        db.OperatorPabiliBrowseCategories.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    static string? Validate(SaveOperatorPabiliBrowseCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Name.Trim().Length > 40)
        {
            return "Name is too long (max 40 characters).";
        }

        return null;
    }

    static OperatorPabiliBrowseCategoryItem Map(OperatorPabiliBrowseCategory row) =>
        new(
            row.Id,
            row.Name,
            row.IsActive,
            row.SortOrder,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
}
