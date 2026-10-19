using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicMapsController(IConfiguration config, AppDbContext db) : ControllerBase
{
    [HttpGet("maps")]
    public ActionResult Maps()
    {
        var key = config["Maps:BrowserApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = config["Maps:GoogleApiKey"] ?? "";
        }

        return Ok(new
        {
            googleMapsBrowserKey = key.Trim(),
            publicOrigin = PublicOrigins.Primary(config)
        });
    }

    [HttpGet("auth")]
    public async Task<ActionResult> Auth(CancellationToken cancellationToken)
    {
        var clientId = (config["GoogleAuth:ClientId"] ?? string.Empty).Trim().Trim('\'').Trim('"').Trim();
        return Ok(new
        {
            googleClientId = clientId,
            phoneSignInEnabled = await PhoneSignInEnabledAsync(cancellationToken),
            publicOrigin = PublicOrigins.Primary(config)
        });
    }

    [HttpGet("branding")]
    public async Task<ActionResult> Branding(CancellationToken cancellationToken)
    {
        var settings = await db.PlatformBrandSettings
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        settings ??= new PlatformBrandSettings
        {
            BrandName = BrandThemeCatalog.DefaultBrandName,
            ShortName = BrandThemeCatalog.DefaultShortName,
            ThemeId = BrandThemeCatalog.DefaultThemeId,
        };

        var theme = BrandThemeCatalog.Resolve(settings.ThemeId);
        return Ok(new
        {
            brandName = settings.BrandName,
            shortName = settings.ShortName,
            logoUrl = UploadUrls.FromPath(settings.LogoPath),
            faviconUrl = UploadUrls.FromPath(settings.FaviconPath),
            themeId = theme.Id,
            accent = theme.Accent,
            good = theme.Good,
        });
    }

    private async Task<bool> PhoneSignInEnabledAsync(CancellationToken cancellationToken)
    {
        return await db.PlatformBrandSettings
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.PhoneSignInEnabled)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
