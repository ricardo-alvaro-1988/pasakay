using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/favorites")]
public class CustomerFavoritesController(AppDbContext db) : ControllerBase
{
    const int MaxFavorites = 50;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerFavoriteRiderCard>>> List(CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await BuildListAsync(customer.Id, cancellationToken));
    }

    [HttpPost("{riderId:guid}")]
    public async Task<ActionResult<CustomerFavoriteRiderCard>> Add(Guid riderId, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var existing = await db.CustomerFavoriteRiders
            .AnyAsync(x => x.CustomerId == customer.Id && x.RiderId == riderId, cancellationToken);
        if (!existing)
        {
            var count = await db.CustomerFavoriteRiders.CountAsync(x => x.CustomerId == customer.Id, cancellationToken);
            if (count >= MaxFavorites)
            {
                return BadRequest(new { message = $"You can save up to {MaxFavorites} favorite riders." });
            }

            var riderOk = await db.RiderProfiles
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == riderId && x.IsActive && x.AcceptsPasakay && x.AppUser.IsActive,
                    cancellationToken);
            if (!riderOk)
            {
                return BadRequest(new { message = "That rider is not available." });
            }

            db.CustomerFavoriteRiders.Add(new CustomerFavoriteRider
            {
                CustomerId = customer.Id,
                RiderId = riderId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var card = (await BuildListAsync(customer.Id, cancellationToken, riderId)).FirstOrDefault();
        if (card is null)
        {
            return BadRequest(new { message = "That rider is not available." });
        }

        return Ok(card);
    }

    [HttpDelete("{riderId:guid}")]
    public async Task<IActionResult> Remove(Guid riderId, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.CustomerFavoriteRiders
            .FirstOrDefaultAsync(x => x.CustomerId == customer.Id && x.RiderId == riderId, cancellationToken);
        if (row is not null)
        {
            db.CustomerFavoriteRiders.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    async Task<List<CustomerFavoriteRiderCard>> BuildListAsync(
        Guid customerId,
        CancellationToken cancellationToken,
        Guid? onlyRiderId = null)
    {
        var query = db.CustomerFavoriteRiders
            .AsNoTracking()
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Include(x => x.Rider)
            .ThenInclude(x => x.Operator)
            .Include(x => x.Rider)
            .ThenInclude(x => x.PaymentMethods)
            .Include(x => x.Rider)
            .ThenInclude(x => x.Wallet)
            .Where(x => x.CustomerId == customerId);
        if (onlyRiderId is Guid filter)
        {
            query = query.Where(x => x.RiderId == filter);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(MaxFavorites)
            .ToListAsync(cancellationToken);

        var riderIds = rows.Select(x => x.RiderId).ToList();
        var busyIds = riderIds.Count == 0
            ? new List<Guid>()
            : await db.Trips
                .AsNoTracking()
                .Where(x => x.RiderId != null && riderIds.Contains(x.RiderId.Value)
                    && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
                .Select(x => x.RiderId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        var busy = busyIds.ToHashSet();

        var cards = new List<CustomerFavoriteRiderCard>();
        foreach (var row in rows)
        {
            var rider = row.Rider;
            if (rider is null || !rider.IsActive || !rider.AcceptsPasakay || !rider.AppUser.IsActive)
            {
                continue;
            }

            var isBusy = busy.Contains(rider.Id);
            var methods = rider.PaymentMethods.Select(m => m.Method).Distinct().OrderBy(m => m).ToList();
            var walletOk = TripBroadcastService.CanReceiveBookings(rider.Wallet?.Balance ?? 0);
            var canBook = rider.IsOnline && !isBusy && walletOk && methods.Count > 0;
            cards.Add(new CustomerFavoriteRiderCard(
                rider.Id,
                rider.AppUser.FullName,
                rider.PlateNumber,
                rider.VehicleType,
                rider.VehicleModel,
                UploadUrls.FromPath(rider.ProfilePhotoPath),
                rider.AppUser.PhoneNumber,
                rider.IsOnline,
                isBusy,
                canBook,
                rider.Operator?.CompanyName ?? "",
                methods));
        }

        return cards;
    }
}
