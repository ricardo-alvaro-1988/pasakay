using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/rider-app")]
public class PublicRiderAppController(AppDbContext db) : ControllerBase
{
    public record RiderAppReleaseDto(
        string Version,
        string DownloadUrl,
        DateTime ReleasedAtUtc,
        string? Notes);

    [HttpGet]
    public async Task<ActionResult<RiderAppReleaseDto>> Latest(CancellationToken cancellationToken)
    {
        var latest = await db.RiderAppReleases
            .AsNoTracking()
            .Where(x => x.IsLatest)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            return NotFound(new { message = "No rider app release has been published yet." });
        }

        return Ok(ToDto(latest));
    }

    [HttpGet("releases")]
    public async Task<ActionResult<IReadOnlyList<RiderAppReleaseDto>>> Releases(CancellationToken cancellationToken)
    {
        var rows = await db.RiderAppReleases
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(ToDto).ToList());
    }

    private static RiderAppReleaseDto ToDto(RiderAppRelease row) =>
        new(
            row.Version,
            UploadUrls.FromPath(row.ApkPath)!,
            row.CreatedAtUtc,
            string.IsNullOrWhiteSpace(row.ReleaseNotes) ? null : row.ReleaseNotes);
}
