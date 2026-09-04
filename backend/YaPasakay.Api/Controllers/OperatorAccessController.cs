using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/access")]
public class OperatorAccessController(AppDbContext db) : ControllerBase
{
    [HttpGet("pages")]
    public ActionResult<IReadOnlyList<AccessPageItem>> Pages() => Ok(OperatorAccessCatalog.Pages);

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<AccessGroupItem>>> Groups(CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var rows = await db.AccessGroups
            .AsNoTracking()
            .Where(x => x.OperatorId == operatorId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                UserCount = x.Users.Count(u => u.Role == UserRole.Operator && u.OperatorId == operatorId),
                Pages = x.Pages.Select(p => p.PageId).ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => new AccessGroupItem(
            x.Id,
            x.Name,
            x.Description,
            x.UserCount,
            x.Pages.Where(OperatorAccessCatalog.IsKnown).ToList())).ToList());
    }

    [HttpPost("groups")]
    public async Task<ActionResult<AccessGroupItem>> CreateGroup(
        [FromBody] SaveAccessGroupRequest request,
        CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var (name, description, pages, error) = ParseGroup(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (await db.AccessGroups.AnyAsync(x => x.OperatorId == operatorId && x.Name == name, cancellationToken))
        {
            return Conflict(new { message = "A role with that name already exists." });
        }

        var group = new AccessGroup
        {
            Name = name,
            Description = description,
            OperatorId = operatorId
        };
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
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var (name, description, pages, error) = ParseGroup(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var group = await db.AccessGroups
            .Include(x => x.Pages)
            .Include(x => x.Users)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (await db.AccessGroups.AnyAsync(x => x.OperatorId == operatorId && x.Name == name && x.Id != id, cancellationToken))
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
        return Ok(ToGroup(group, group.Users.Count(x => x.Role == UserRole.Operator && x.OperatorId == operatorId)));
    }

    [HttpPost("groups/{id:guid}/delete")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var group = await db.AccessGroups
            .Include(x => x.Users)
            .Include(x => x.Pages)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (group.Users.Any(x => x.Role == UserRole.Operator && x.OperatorId == operatorId))
        {
            return BadRequest(new { message = "Move employees off this role before deleting it." });
        }

        db.AccessGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AccessStaffItem>>> Users(CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var rows = await db.Users
            .AsNoTracking()
            .Where(x => x.Role == UserRole.Operator && x.OperatorId == operatorId)
            .OrderByDescending(x => x.IsMainOperator)
            .ThenBy(x => x.FullName)
            .Select(x => new AccessStaffItem(
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.AccessGroupId ?? Guid.Empty,
                x.IsMainOperator ? "Main operator" : (x.AccessGroup != null ? x.AccessGroup.Name : ""),
                x.IsActive,
                false,
                x.CreatedAtUtc,
                x.IsMainOperator))
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => x with { CreatedAtUtc = DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc) }).ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<AccessStaffItem>> CreateUser(
        [FromBody] SaveAccessStaffRequest request,
        CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var (name, phone, group, error) = await ParseStaffAsync(request, operatorId, null, cancellationToken);
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
            Role = UserRole.Operator,
            OperatorId = operatorId,
            AccessGroupId = group.Id,
            IsMainOperator = false,
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
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Id == id && x.Role == UserRole.Operator && x.OperatorId == operatorId,
            cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsMainOperator)
        {
            return BadRequest(new { message = "The main operator account cannot be changed here." });
        }

        var (name, phone, group, error) = await ParseStaffAsync(request, operatorId, id, cancellationToken);
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
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var user = await db.Users
            .Include(x => x.AccessGroup)
            .FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Operator && x.OperatorId == operatorId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsMainOperator)
        {
            return BadRequest(new { message = "The main operator account cannot be deactivated." });
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStaff(user, user.IsMainOperator ? "Main operator" : user.AccessGroup?.Name ?? ""));
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetPassword(
        Guid id,
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var gate = await RequireMainAsync(cancellationToken);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var operatorId = gate.OperatorId!.Value;
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Id == id && x.Role == UserRole.Operator && x.OperatorId == operatorId,
            cancellationToken);
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

    private async Task<(Guid? OperatorId, ActionResult? Error)> RequireMainAsync(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return (null, StatusCode(status, new { message }));
        }

        var userId = AdminAccess.UserId(User);
        var user = userId is Guid id
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            : null;
        if (user is null || !user.IsMainOperator)
        {
            return (null, StatusCode(403, new { message = "Only the main operator can manage employees and roles." }));
        }

        return (op.Id, null);
    }

    private async Task<(string Name, string Phone, AccessGroup? Group, string? Error)> ParseStaffAsync(
        SaveAccessStaffRequest request,
        Guid operatorId,
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

        var group = await db.AccessGroups.FirstOrDefaultAsync(
            x => x.Id == request.AccessGroupId && x.OperatorId == operatorId,
            cancellationToken);
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
            .Where(OperatorAccessCatalog.IsKnown)
            .Distinct()
            .ToList();
        if (pages.Count == 0)
        {
            return ("", "", [], "Assign at least one module.");
        }

        return (name, description, pages, null);
    }

    private static AccessGroupItem ToGroup(AccessGroup group, int userCount) =>
        new(
            group.Id,
            group.Name,
            group.Description,
            userCount,
            group.Pages.Select(x => x.PageId).Where(OperatorAccessCatalog.IsKnown).ToList());

    private static AccessStaffItem ToStaff(AppUser user, string groupName) =>
        new(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.AccessGroupId ?? Guid.Empty,
            user.IsMainOperator ? "Main operator" : groupName,
            user.IsActive,
            false,
            DateTime.SpecifyKind(user.CreatedAtUtc, DateTimeKind.Utc),
            user.IsMainOperator);
}
