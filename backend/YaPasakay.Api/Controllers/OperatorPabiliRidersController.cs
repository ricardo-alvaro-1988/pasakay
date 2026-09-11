using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/pabili-riders")]
public class OperatorPabiliRidersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<RiderListItem>>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = LinkedQuery(op.Id, q);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.AppUser.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<RiderListItem>(rows.Select(OperatorMaps.Rider).ToList(), page, pageSize, total));
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<PagedResult<RiderListItem>>> Candidates(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = CandidateQuery(op.Id, q);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.AppUser.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<RiderListItem>(rows.Select(OperatorMaps.Rider).ToList(), page, pageSize, total));
    }

    [HttpPost("{id:guid}/link")]
    public async Task<ActionResult<RiderListItem>> Link(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (rider is null)
        {
            return NotFound(new { message = "Rider not found." });
        }

        if (!rider.AcceptsPabili)
        {
            rider.AcceptsPabili = true;
            rider.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(OperatorMaps.Rider(rider));
    }

    [HttpPost("{id:guid}/unlink")]
    public async Task<ActionResult<RiderListItem>> Unlink(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (rider is null)
        {
            return NotFound(new { message = "Rider not found." });
        }

        if (rider.AcceptsPabili)
        {
            rider.AcceptsPabili = false;
            rider.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(OperatorMaps.Rider(rider));
    }

    private IQueryable<Domain.Entities.RiderProfile> LinkedQuery(Guid operatorId, string? q)
    {
        var query = db.RiderProfiles
            .AsNoTracking()
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .Where(x => x.OperatorId == operatorId && x.AcceptsPabili);

        return ApplySearch(query, q);
    }

    private IQueryable<Domain.Entities.RiderProfile> CandidateQuery(Guid operatorId, string? q)
    {
        var query = db.RiderProfiles
            .AsNoTracking()
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .Where(x => x.OperatorId == operatorId && !x.AcceptsPabili);

        return ApplySearch(query, q);
    }

    private static IQueryable<Domain.Entities.RiderProfile> ApplySearch(
        IQueryable<Domain.Entities.RiderProfile> query,
        string? q)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return query;
        }

        return query.Where(x =>
            x.AppUser.FullName.Contains(term)
            || x.AppUser.PhoneNumber.Contains(term)
            || x.PlateNumber.Contains(term));
    }
}
