using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/access")]
public class AccessController(AppDbContext db) : ControllerBase
{
    [HttpGet("pages")]
    public ActionResult<IReadOnlyList<AccessPageItem>> Pages() => Ok(AccessCatalog.Pages);

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<AccessGroupItem>>> Groups(CancellationToken cancellationToken)
    {
        var rows = await db.AccessGroups
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                UserCount = x.Users.Count(u => u.Role == UserRole.Admin),
                Pages = x.Pages.Select(p => p.PageId).ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => new AccessGroupItem(
            x.Id,
            x.Name,
            x.Description,
            x.UserCount,
            x.Pages.Where(AccessCatalog.IsKnown).ToList())).ToList());
    }

    [HttpPost("groups")]
    public async Task<ActionResult<AccessGroupItem>> CreateGroup(
        [FromBody] SaveAccessGroupRequest request,
        CancellationToken cancellationToken)
    {
        var (name, description, pages, error) = ParseGroup(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (await db.AccessGroups.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Conflict(new { message = "A role with that name already exists." });
        }

        var group = new AccessGroup { Name = name, Description = description };
        foreach (var page in pages)
        {
            group.Pages.Add(new AccessGroupPage { PageId = page });
        }

        db.AccessGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToGroup(group, 0));
    }

    [HttpPut("groups/{id:guid}")]
    public async Task<ActionResult<AccessGroupItem>> UpdateGroup(
        Guid id,
        [FromBody] SaveAccessGroupRequest request,
        CancellationToken cancellationToken)
    {
        var (name, description, pages, error) = ParseGroup(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var group = await db.AccessGroups
            .Include(x => x.Pages)
            .Include(x => x.Users)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (await db.AccessGroups.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken))
        {
            return Conflict(new { message = "A role with that name already exists." });
        }

        group.Name = name;
        group.Description = description;
        group.UpdatedAtUtc = DateTime.UtcNow;
        db.AccessGroupPages.RemoveRange(group.Pages);
        foreach (var page in pages)
        {
            group.Pages.Add(new AccessGroupPage { PageId = page });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToGroup(group, group.Users.Count(x => x.Role == UserRole.Admin)));
    }

    [HttpPost("groups/{id:guid}/delete")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken)
    {
        var group = await db.AccessGroups
            .Include(x => x.Users)
            .Include(x => x.Pages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (group.Users.Any(x => x.Role == UserRole.Admin))
        {
            return BadRequest(new { message = "Move users off this role before deleting it." });
        }

        db.AccessGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AccessStaffItem>>> Users(CancellationToken cancellationToken)
    {
        var rows = await db.Users
            .AsNoTracking()
            .Where(x => x.Role == UserRole.Admin)
            .OrderByDescending(x => x.IsMainAdmin)
            .ThenBy(x => x.FullName)
            .Select(x => new AccessStaffItem(
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.AccessGroupId ?? Guid.Empty,
                x.IsMainAdmin ? "Administrator" : (x.AccessGroup != null ? x.AccessGroup.Name : ""),
                x.IsActive,
                x.IsMainAdmin,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => x with { CreatedAtUtc = DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc) }).ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<AccessStaffItem>> CreateUser(
        [FromBody] SaveAccessStaffRequest request,
        CancellationToken cancellationToken)
    {
        var (name, phone, group, error) = await ParseStaffAsync(request, null, cancellationToken);
        if (error is not null || group is null)
        {
            return BadRequest(new { message = error ?? "Choose a role." });
        }

        if (!SecretHasher.IsStrongPassword(request.Password ?? string.Empty))
        {
            return BadRequest(new { message = "Password must be at least 6 characters." });
        }

        var user = new AppUser
        {
            FullName = name,
            PhoneNumber = phone,
            PasswordHash = SecretHasher.Hash(request.Password!.Trim()),
            Role = UserRole.Admin,
            AccessGroupId = group.Id,
            IsMainAdmin = false,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStaff(user, group.Name));
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AccessStaffItem>> UpdateUser(
        Guid id,
        [FromBody] SaveAccessStaffRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Admin, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsMainAdmin)
        {
            return BadRequest(new { message = "The main admin account cannot be changed here." });
        }

        var (name, phone, group, error) = await ParseStaffAsync(request, id, cancellationToken);
        if (error is not null || group is null)
        {
            return BadRequest(new { message = error ?? "Choose a role." });
        }

        user.FullName = name;
        user.PhoneNumber = phone;
        user.AccessGroupId = group.Id;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (!SecretHasher.IsStrongPassword(request.Password))
            {
                return BadRequest(new { message = "Password must be at least 6 characters." });
            }

            user.PasswordHash = SecretHasher.Hash(request.Password.Trim());
        }
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStaff(user, group.Name));
    }

    [HttpPost("users/{id:guid}/active")]
    public async Task<ActionResult<AccessStaffItem>> SetActive(
        Guid id,
        [FromBody] SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(x => x.AccessGroup)
            .FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Admin, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsMainAdmin)
        {
            return BadRequest(new { message = "The main admin account cannot be deactivated." });
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStaff(user, user.IsMainAdmin ? "Administrator" : user.AccessGroup?.Name ?? ""));
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetPassword(
        Guid id,
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Admin, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var (result, error) = await LoginReset.SetPasswordAsync(db, user, request.Password, cancellationToken);
        if (error is not null || result is null)
        {
            return BadRequest(new { message = error ?? "Could not set password." });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    private async Task<(string Name, string Phone, AccessGroup? Group, string? Error)> ParseStaffAsync(
        SaveAccessStaffRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var name = (request.FullName ?? string.Empty).Trim();
        var phone = PhoneNormalizer.Normalize(request.Phone);
        if (name.Length == 0 || phone.Length < 10)
        {
            return ("", "", null, "Name and a valid phone number are required.");
        }

        var taken = await db.Users.AnyAsync(
            x => x.PhoneNumber == phone && (userId == null || x.Id != userId),
            cancellationToken);
        if (taken)
        {
            return ("", "", null, "That phone is already in use.");
        }

        var group = await db.AccessGroups.FirstOrDefaultAsync(x => x.Id == request.AccessGroupId, cancellationToken);
        if (group is null)
        {
            return ("", "", null, "Choose a role.");
        }

        return (name, phone, group, null);
    }

    private static (string Name, string Description, List<string> Pages, string? Error) ParseGroup(SaveAccessGroupRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ("", "", [], "Role name is required.");
        }

        var pages = (request.Pages ?? [])
            .Select(x => (x ?? string.Empty).Trim().ToLowerInvariant())
            .Where(AccessCatalog.IsKnown)
            .Distinct()
            .ToList();
        if (pages.Count == 0)
        {
            return ("", "", [], "Assign at least one module.");
        }

        return (name, description, pages, null);
    }

    private static AccessGroupItem ToGroup(AccessGroup group, int userCount) =>
        new(group.Id, group.Name, group.Description, userCount, group.Pages.Select(x => x.PageId).Where(AccessCatalog.IsKnown).ToList());

    private static AccessStaffItem ToStaff(AppUser user, string groupName) =>
        new(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.AccessGroupId ?? Guid.Empty,
            user.IsMainAdmin ? "Administrator" : groupName,
            user.IsActive,
            user.IsMainAdmin,
            DateTime.SpecifyKind(user.CreatedAtUtc, DateTimeKind.Utc));
}
