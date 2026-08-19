using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;
using YaPasakay.Infrastructure.Services;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Rider,Customer")]
[Route("api/sos")]
public class SosController(AppDbContext db, LiveNotify live) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupportTicketItem>> Raise(
        [FromBody] CreateSosRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var appUserId))
        {
            return Unauthorized();
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == appUserId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var trip = await db.Trips
            .Include(x => x.Operator)
            .Include(x => x.Rider)
                .ThenInclude(x => x!.AppUser)
            .Include(x => x.Customer)
                .ThenInclude(x => x!.AppUser)
            .Include(x => x.PickupBarangay)
                .ThenInclude(x => x!.Municipality)
            .FirstOrDefaultAsync(x => x.Id == request.TripId, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        SupportOpenedBy openedBy;
        Guid? riderId = null;
        Guid? customerId = null;

        if (user.Role == UserRole.Rider)
        {
            var rider = await db.RiderProfiles.FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);
            if (rider is null || rider.Id != trip.RiderId)
            {
                return Forbid();
            }

            openedBy = SupportOpenedBy.Rider;
            riderId = rider.Id;
        }
        else if (user.Role == UserRole.Customer)
        {
            var customer = await db.CustomerProfiles.FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);
            if (customer is null)
            {
                return Forbid();
            }

            if (trip.CustomerId != customer.Id)
            {
                return Forbid();
            }

            openedBy = SupportOpenedBy.Customer;
            customerId = customer.Id;
        }
        else
        {
            return Forbid();
        }

        var (ticket, error) = await SosAlerts.RaiseAsync(
            db,
            trip,
            openedBy,
            riderId,
            customerId,
            request.Message,
            request.Lat,
            request.Lng,
            cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        await live.SosPushAsync(trip, ticket!, cancellationToken);

        var loaded = await db.SupportTickets
            .Include(x => x.Operator)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.PickupBarangay)
                    .ThenInclude(x => x!.Municipality)
            .Include(x => x.Rider)
                .ThenInclude(x => x!.AppUser)
            .Include(x => x.Customer)
                .ThenInclude(x => x!.AppUser)
            .AsNoTracking()
            .FirstAsync(x => x.Id == ticket!.Id, cancellationToken);

        return Ok(OperatorMaps.Support(loaded));
    }
}
