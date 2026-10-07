using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/passenger-app")]
public class PublicPassengerAppController(AppDbContext db) : ControllerBase
{
    public record PassengerAppReleaseDto(
        string Version,
        string DownloadUrl,
        DateTime ReleasedAtUtc,
        string? Notes);

    [HttpGet]
    public async Task<ActionResult<PassengerAppReleaseDto>> Latest(CancellationToken cancellationToken)
    {
        var latest = await db.PassengerAppReleases
            .AsNoTracking()
            .Where(x => x.IsLatest)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            return NotFound(new { message = "No passenger app release has been published yet." });
        }

        return Ok(ToDto(latest));
    }

    [HttpGet("releases")]
    public async Task<ActionResult<IReadOnlyList<PassengerAppReleaseDto>>> Releases(CancellationToken cancellationToken)
    {
        var rows = await db.PassengerAppReleases
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(ToDto).ToList());
    }

    private static PassengerAppReleaseDto ToDto(PassengerAppRelease row) =>
        new(
            row.Version,
            UploadUrls.FromPath(row.ApkPath)!,
            row.CreatedAtUtc,
            string.IsNullOrWhiteSpace(row.ReleaseNotes) ? null : row.ReleaseNotes);
}
