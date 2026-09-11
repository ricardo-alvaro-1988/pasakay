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
[Route("api/operator/ads")]
public class OperatorAdsController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorAdListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.OperatorAds
            .AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return Ok(new OperatorAdListResponse(rows.Select(Map).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<OperatorAdItem>> Create(
        [FromBody] SaveOperatorAdRequest request,
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

        var row = new OperatorAd
        {
            OperatorId = op.Id,
            Title = request.Title.Trim(),
            RedirectUrl = request.RedirectUrl.Trim(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
        };
        db.OperatorAds.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperatorAdItem>> Update(
        Guid id,
        [FromBody] SaveOperatorAdRequest request,
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

        var row = await db.OperatorAds.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Ad not found." });
        }

        row.Title = request.Title.Trim();
        row.RedirectUrl = request.RedirectUrl.Trim();
        row.IsActive = request.IsActive;
        row.SortOrder = request.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<OperatorAdItem>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorAds.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Ad not found." });
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

        var row = await db.OperatorAds.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Ad not found." });
        }

        db.OperatorAds.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/image")]
    public async Task<ActionResult<OperatorAdItem>> UploadImage(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorAds.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Ad not found." });
        }

        try
        {
            var path = await uploads.SaveAsync(file, $"ads/{row.Id}", "creative", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Image file is required." });
            }

            row.ImagePath = path;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(Map(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    static string? Validate(SaveOperatorAdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Title is required.";
        }

        if (request.Title.Trim().Length > 120)
        {
            return "Title is too long.";
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUrl))
        {
            return "Redirect URL is required.";
        }

        if (!Uri.TryCreate(request.RedirectUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Redirect URL must be a valid http or https link.";
        }

        return null;
    }

    static OperatorAdItem Map(OperatorAd row) =>
        new(
            row.Id,
            row.Title,
            UploadUrls.FromPath(row.ImagePath),
            row.RedirectUrl,
            row.IsActive,
            row.SortOrder,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
}
