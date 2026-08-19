using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/devices")]
public class DevicesController(AppDbContext db) : ControllerBase
{
    public record RegisterDeviceRequest(string Token, string? Platform);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var token = (request.Token ?? string.Empty).Trim();
        if (token.Length < 8)
        {
            return BadRequest(new { message = "Device token is required." });
        }

        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "Unknown" : request.Platform.Trim();
        if (platform.Length > 40)
        {
            platform = platform[..40];
        }

        var existing = await db.DeviceRegistrations.FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (existing is null)
        {
            db.DeviceRegistrations.Add(new DeviceRegistration
            {
                AppUserId = userId.Value,
                Token = token,
                Platform = platform,
                LastSeenAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.AppUserId = userId.Value;
            existing.Platform = platform;
            existing.LastSeenAtUtc = DateTime.UtcNow;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Device registered." });
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var token = (request.Token ?? string.Empty).Trim();
        var row = await db.DeviceRegistrations.FirstOrDefaultAsync(
            x => x.Token == token && x.AppUserId == userId,
            cancellationToken);
        if (row is not null)
        {
            db.DeviceRegistrations.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Device unregistered." });
    }
}
