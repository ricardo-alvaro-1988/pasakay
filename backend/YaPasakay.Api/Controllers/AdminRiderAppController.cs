using System.Text.RegularExpressions;
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
[Route("api/admin/rider-app")]
public class AdminRiderAppController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    private static readonly Regex VersionPattern = new(
        @"^\d+(\.\d+){1,3}([+-][A-Za-z0-9.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public record RiderAppReleaseDto(
        Guid Id,
        string Version,
        string DownloadUrl,
        DateTime ReleasedAtUtc,
        string? Notes,
        bool IsLatest);

    public record RiderAppAdminDto(
        RiderAppReleaseDto? Latest,
        IReadOnlyList<RiderAppReleaseDto> Releases);

    [HttpGet]
    public async Task<ActionResult<RiderAppAdminDto>> Get(CancellationToken cancellationToken)
    {
        var rows = await db.RiderAppReleases
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);
        var latest = rows.FirstOrDefault(x => x.IsLatest) ?? rows.FirstOrDefault();
        return Ok(new RiderAppAdminDto(
            latest is null ? null : ToDto(latest),
            rows.Select(ToDto).ToList()));
    }

    [HttpPost]
    [RequestSizeLimit(UploadStore.MaxApkBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadStore.MaxApkBytes)]
    public async Task<ActionResult<RiderAppAdminDto>> Publish(
        IFormFile? file,
        [FromForm] string version,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var cleanedVersion = (version ?? string.Empty).Trim();
        if (cleanedVersion.Length is < 3 or > 40 || !VersionPattern.IsMatch(cleanedVersion))
        {
            return BadRequest(new { message = "Version must look like 1.4.2 (optional build suffix)." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose an Android APK file." });
        }

        var exists = await db.RiderAppReleases.AnyAsync(x => x.Version == cleanedVersion, cancellationToken);
        if (exists)
        {
            return BadRequest(new { message = $"Version {cleanedVersion} is already published." });
        }

        var releaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (releaseNotes is { Length: > 2000 })
        {
            return BadRequest(new { message = "Release notes must be at most 2000 characters." });
        }

        var row = new RiderAppRelease
        {
            Version = cleanedVersion,
            ReleaseNotes = releaseNotes,
            IsLatest = true,
            ApkPath = string.Empty,
        };

        try
        {
            var path = await uploads.SaveApkAsync(file, $"rider-apk/{row.Id}", "app", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Could not save the APK." });
            }

            row.ApkPath = path;
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var previous = await db.RiderAppReleases.Where(x => x.IsLatest).ToListAsync(cancellationToken);
            foreach (var prior in previous)
            {
                prior.IsLatest = false;
                prior.UpdatedAtUtc = DateTime.UtcNow;
            }

            db.RiderAppReleases.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return await Get(cancellationToken);
    }

    private static RiderAppReleaseDto ToDto(RiderAppRelease row) =>
        new(
            row.Id,
            row.Version,
            UploadUrls.FromPath(row.ApkPath)!,
            row.CreatedAtUtc,
            string.IsNullOrWhiteSpace(row.ReleaseNotes) ? null : row.ReleaseNotes,
            row.IsLatest);
}
