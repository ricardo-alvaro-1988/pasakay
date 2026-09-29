using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/customers")]
public class OperatorCustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerListItem>>> List(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var relatedIds = db.Trips
            .Where(x => x.OperatorId == op!.Id && x.CustomerId != null)
            .Select(x => x.CustomerId!.Value)
            .Distinct();

        var query = db.CustomerProfiles
            .Include(x => x.AppUser)
            .Where(x => relatedIds.Contains(x.Id));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.FirstName.Contains(term) ||
                x.LastName.Contains(term) ||
                x.AppUser.FullName.Contains(term) ||
                x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term) ||
                (x.AppUser.Email != null && x.AppUser.Email.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(x => x.DeleteStatus == DeleteAccountStatus.Pending)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(OperatorMaps.Customer).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        if (!await RelatedAsync(op!.Id, id, cancellationToken))
        {
            return NotFound();
        }

        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return customer is null ? NotFound() : Ok(OperatorMaps.CustomerDetail(customer));
    }

    [HttpGet("{id:guid}/rides")]
    public async Task<ActionResult<RiderRidesResponse>> Rides(
        Guid id,
        [FromQuery] string range = "weekly",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? q = null,
        [FromQuery] TripStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, statusCode, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(statusCode, new { message });
        }

        if (!await RelatedAsync(op!.Id, id, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await OperatorMaps.BuildRidesAsync(
            db.Trips.Where(x => x.OperatorId == op.Id && x.CustomerId == id),
            db,
            range,
            from,
            to,
            q,
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:guid}/rides/{rideId:guid}")]
    public async Task<ActionResult<RideDetailResponse>> Ride(Guid id, Guid rideId, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(
                x => x.OperatorId == op!.Id && x.CustomerId == id && x.Id == rideId,
                cancellationToken);
        return trip is null ? NotFound() : Ok(await OperatorMaps.RideDetailAsync(trip, db, cancellationToken));
    }

    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<CustomerDetailResponse>> Block(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var actorId = AdminAccess.UserId(User);
        if (actorId is null)
        {
            return Unauthorized();
        }

        var customer = await LoadRelatedAsync(op.Id, id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (customer.AppUser.IsLoginBlocked)
        {
            return Ok(OperatorMaps.CustomerDetail(customer));
        }

        var email = LoginBlocks.NormalizeEmail(customer.AppUser.Email);
        var phone = LoginBlocks.NormalizePhone(customer.AppUser.PhoneNumber);
        if (email is null && phone is null)
        {
            return BadRequest(new { message = "This customer has no email or mobile number to block." });
        }

        var now = DateTime.UtcNow;
        db.CustomerLoginBlocks.Add(new CustomerLoginBlock
        {
            AppUserId = customer.AppUserId,
            Email = email,
            PhoneNumber = phone,
            OperatorId = op.Id,
            BlockedByUserId = actorId.Value,
            CreatedAtUtc = now
        });

        customer.AppUser.IsLoginBlocked = true;
        customer.AppUser.IsActive = false;
        customer.AppUser.UpdatedAtUtc = now;

        var tokens = await db.RefreshTokens
            .Where(x => x.AppUserId == customer.AppUserId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAtUtc = now;
        }

        OperatorAudit.Record(
            db,
            User,
            op.Id,
            AuditAction.CustomerBlocked,
            $"Blocked customer {OperatorMaps.CustomerDisplayName(customer)}. Their email or mobile cannot sign in.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(OperatorMaps.CustomerDetail(customer));
    }

    [HttpPost("{id:guid}/unblock")]
    public async Task<ActionResult<CustomerDetailResponse>> Unblock(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var customer = await LoadRelatedAsync(op.Id, id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (!customer.AppUser.IsLoginBlocked)
        {
            return BadRequest(new { message = "This customer is not blocked." });
        }

        var now = DateTime.UtcNow;
        var blocks = await db.CustomerLoginBlocks
            .Where(x => x.AppUserId == customer.AppUserId && x.LiftedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var block in blocks)
        {
            block.LiftedAtUtc = now;
            block.UpdatedAtUtc = now;
        }

        customer.AppUser.IsLoginBlocked = false;
        customer.AppUser.UpdatedAtUtc = now;
        if (customer.DeleteStatus != DeleteAccountStatus.Approved)
        {
            customer.AppUser.IsActive = true;
        }

        OperatorAudit.Record(
            db,
            User,
            op.Id,
            AuditAction.CustomerUnblocked,
            $"Unblocked customer {OperatorMaps.CustomerDisplayName(customer)}. They can sign in again.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(OperatorMaps.CustomerDetail(customer));
    }

    private async Task<CustomerProfile?> LoadRelatedAsync(Guid operatorId, Guid customerId, CancellationToken cancellationToken)
    {
        if (!await RelatedAsync(operatorId, customerId, cancellationToken))
        {
            return null;
        }

        return await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
    }

    private Task<bool> RelatedAsync(Guid operatorId, Guid customerId, CancellationToken cancellationToken) =>
        db.Trips.AnyAsync(x => x.OperatorId == operatorId && x.CustomerId == customerId, cancellationToken);
}
