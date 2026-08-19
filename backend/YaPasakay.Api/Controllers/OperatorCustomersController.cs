using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
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
                x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term));
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
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
    }

    private Task<bool> RelatedAsync(Guid operatorId, Guid customerId, CancellationToken cancellationToken) =>
        db.Trips.AnyAsync(x => x.OperatorId == operatorId && x.CustomerId == customerId, cancellationToken);
}
