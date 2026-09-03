using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/branding")]
public class BrandingController(AppDbContext db, UploadStore uploads, BrandShell brandShell) : ControllerBase
{
    public record BrandingDto(
        string BrandName,
        string ShortName,
        string? LogoUrl,
        string? FaviconUrl,
        string ThemeId,
        string Accent,
        string Good,
        IReadOnlyList<BrandThemePreset> Themes);

    public record UpdateBrandingRequest(
        string BrandName,
        string? ShortName,
        string ThemeId,
        bool? ClearLogo,
        bool? ClearFavicon);

    [HttpGet]
    public async Task<ActionResult<BrandingDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await EnsureAsync(cancellationToken);
        return Ok(ToDto(settings));
    }

    [HttpPut]
    public async Task<ActionResult<BrandingDto>> Update([FromBody] UpdateBrandingRequest request, CancellationToken cancellationToken)
    {
        var brandName = (request.BrandName ?? string.Empty).Trim();
        if (brandName.Length is < 2 or > 80)
        {
            return BadRequest(new { message = "Brand name must be 2–80 characters." });
        }

        var shortName = string.IsNullOrWhiteSpace(request.ShortName)
            ? brandName
            : request.ShortName.Trim();
        if (shortName.Length > 40)
        {
            return BadRequest(new { message = "Short name must be at most 40 characters." });
        }

        if (!BrandThemeCatalog.IsKnown(request.ThemeId))
        {
            return BadRequest(new { message = "Pick a theme from the catalog." });
        }

        var settings = await EnsureAsync(cancellationToken);
        settings.BrandName = brandName;
        settings.ShortName = shortName;
        settings.ThemeId = request.ThemeId.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;
        if (request.ClearLogo == true)
        {
            settings.LogoPath = null;
        }

        if (request.ClearFavicon == true)
        {
            settings.FaviconPath = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        brandShell.Invalidate();
        return Ok(ToDto(settings));
    }

    [HttpPost("logo")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<BrandingDto>> UploadLogo(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a logo image." });
        }

        try
        {
            var settings = await EnsureAsync(cancellationToken);
            var path = await uploads.SaveAsync(file, "brand", "logo", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Could not save logo." });
            }

            settings.LogoPath = path;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            brandShell.Invalidate();
            return Ok(ToDto(settings));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("favicon")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<BrandingDto>> UploadFavicon(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a favicon image (PNG, JPG, WEBP, or ICO)." });
        }

        try
        {
            var settings = await EnsureAsync(cancellationToken);
            var path = await uploads.SaveAsync(file, "brand", "favicon", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Could not save favicon." });
            }

            settings.FaviconPath = path;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            brandShell.Invalidate();
            return Ok(ToDto(settings));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<PlatformBrandSettings> EnsureAsync(CancellationToken cancellationToken)
    {
        var settings = await db.PlatformBrandSettings.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new PlatformBrandSettings
        {
            BrandName = BrandThemeCatalog.DefaultBrandName,
            ShortName = BrandThemeCatalog.DefaultShortName,
            ThemeId = BrandThemeCatalog.DefaultThemeId,
        };
        db.PlatformBrandSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static BrandingDto ToDto(PlatformBrandSettings settings)
    {
        var theme = BrandThemeCatalog.Resolve(settings.ThemeId);
        return new BrandingDto(
            settings.BrandName,
            settings.ShortName,
            UploadUrls.FromPath(settings.LogoPath),
            UploadUrls.FromPath(settings.FaviconPath),
            theme.Id,
            theme.Accent,
            theme.Good,
            BrandThemeCatalog.All);
    }
}
