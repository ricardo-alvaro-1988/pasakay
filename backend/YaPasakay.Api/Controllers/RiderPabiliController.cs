using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Rider")]
[Route("api/rider/pabili")]
public class RiderPabiliController(
    AppDbContext db,
    LiveNotify live,
    RiderWalletService wallets) : ControllerBase
{
    [HttpGet("offers")]
    public async Task<ActionResult<IReadOnlyList<RiderPabiliOfferItem>>> Offers(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var now = DateTime.UtcNow;
        var rows = await db.PabiliOrderOffers.AsNoTracking()
            .Include(x => x.Order)
            .Where(x => x.RiderId == rider.Id
                && x.Status == OfferStatus.Offered
                && x.ExpiresAtUtc > now
                && x.Order.Status == PabiliOrderStatus.Pending)
            .OrderBy(x => x.OfferedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => new RiderPabiliOfferItem(
            x.Id,
            x.OrderId,
            x.Order.Reference,
            x.Order.Status.ToString(),
            x.Order.MerchantName,
            x.Order.PickupAddress,
            x.Order.DropoffAddress,
            x.Order.CustomerTotal,
            x.Order.DeliveryFee,
            x.DistanceKm is decimal km ? (double)km : null,
            DateTime.SpecifyKind(x.ExpiresAtUtc, DateTimeKind.Utc))).ToList());
    }

    [HttpPost("offers/{id:guid}/accept")]
    public async Task<ActionResult<RiderPabiliOrderDetail>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var offer = await db.PabiliOrderOffers
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (offer is null)
        {
            return NotFound(new { message = "Offer not found." });
        }

        if (offer.Status != OfferStatus.Offered || offer.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "This offer is no longer available." });
        }

        if (offer.Order.Status != PabiliOrderStatus.Pending)
        {
            return BadRequest(new { message = "Order is no longer pending." });
        }

        if (await IsBusyAsync(rider.Id, cancellationToken))
        {
            return BadRequest(new { message = "Finish your current trip or Pabili order first." });
        }

        var now = DateTime.UtcNow;
        offer.Status = OfferStatus.Accepted;
        offer.RespondedAtUtc = now;
        offer.UpdatedAtUtc = now;
        offer.Order.Status = PabiliOrderStatus.Waiting;
        offer.Order.RiderId = rider.Id;
        offer.Order.AcceptedAtUtc = now;
        offer.Order.UpdatedAtUtc = now;

        var others = await db.PabiliOrderOffers
            .Where(x => x.OrderId == offer.OrderId && x.Id != offer.Id && x.Status == OfferStatus.Offered)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.Status = OfferStatus.Expired;
            other.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await live.CustomerChangedAsync(offer.Order.CustomerId, "pabili-accepted", cancellationToken);
        await live.RiderChangedAsync(rider.Id, "pabili-accepted", cancellationToken);
        return Ok(await LoadOrderAsync(offer.OrderId, rider.Id, cancellationToken));
    }

    [HttpPost("offers/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var offer = await db.PabiliOrderOffers
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (offer is null)
        {
            return NotFound(new { message = "Offer not found." });
        }

        if (offer.Status == OfferStatus.Offered)
        {
            offer.Status = OfferStatus.Declined;
            offer.RespondedAtUtc = DateTime.UtcNow;
            offer.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Declined." });
    }

    [HttpGet("orders/active")]
    public async Task<ActionResult<RiderPabiliOrderDetail?>> Active(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var id = await db.PabiliOrders.AsNoTracking()
            .Where(x => x.RiderId == rider.Id
                && (x.Status == PabiliOrderStatus.Waiting
                    || x.Status == PabiliOrderStatus.PickedUp
                    || x.Status == PabiliOrderStatus.Delivering))
            .OrderByDescending(x => x.AcceptedAtUtc)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (id == Guid.Empty)
        {
            return Ok(null);
        }

        return Ok(await LoadOrderAsync(id, rider.Id, cancellationToken));
    }

    [HttpPost("orders/{id:guid}/picked-up")]
    public async Task<ActionResult<RiderPabiliOrderDetail>> PickedUp(Guid id, CancellationToken cancellationToken)
    {
        return await AdvanceAsync(id, PabiliOrderStatus.Waiting, PabiliOrderStatus.PickedUp, cancellationToken);
    }

    [HttpPost("orders/{id:guid}/delivering")]
    public async Task<ActionResult<RiderPabiliOrderDetail>> Delivering(Guid id, CancellationToken cancellationToken)
    {
        return await AdvanceAsync(id, PabiliOrderStatus.PickedUp, PabiliOrderStatus.Delivering, cancellationToken);
    }

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<ActionResult<RiderPabiliOrderDetail>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var order = await db.PabiliOrders
            .Include(x => x.Operator)
            .FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (order.Status != PabiliOrderStatus.Delivering)
        {
            return BadRequest(new { message = "Mark the order as delivering before completing." });
        }

        order.Status = PabiliOrderStatus.Completed;
        order.CompletedAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await wallets.ApplyPabiliCommissionAsync(order, cancellationToken);
        await live.CustomerChangedAsync(order.CustomerId, "pabili-completed", cancellationToken);
        await live.RiderChangedAsync(rider.Id, "pabili-completed", cancellationToken);
        return Ok(await LoadOrderAsync(order.Id, rider.Id, cancellationToken));
    }

    private async Task<ActionResult<RiderPabiliOrderDetail>> AdvanceAsync(
        Guid id,
        PabiliOrderStatus from,
        PabiliOrderStatus to,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var order = await db.PabiliOrders.FirstOrDefaultAsync(x => x.Id == id && x.RiderId == rider.Id, cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (order.Status != from)
        {
            return BadRequest(new { message = $"Order must be {from} before moving to {to}." });
        }

        var now = DateTime.UtcNow;
        order.Status = to;
        order.UpdatedAtUtc = now;
        if (to == PabiliOrderStatus.PickedUp)
        {
            order.PickedUpAtUtc = now;
        }
        else if (to == PabiliOrderStatus.Delivering)
        {
            order.DeliveringAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await live.CustomerChangedAsync(order.CustomerId, $"pabili-{to.ToString().ToLowerInvariant()}", cancellationToken);
        return Ok(await LoadOrderAsync(order.Id, rider.Id, cancellationToken));
    }

    private async Task<bool> IsBusyAsync(Guid riderId, CancellationToken cancellationToken)
    {
        var tripBusy = await db.Trips.AnyAsync(
            x => x.RiderId == riderId && (x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing),
            cancellationToken);
        if (tripBusy)
        {
            return true;
        }

        return await db.PabiliOrders.AnyAsync(
            x => x.RiderId == riderId
                && (x.Status == PabiliOrderStatus.Waiting
                    || x.Status == PabiliOrderStatus.PickedUp
                    || x.Status == PabiliOrderStatus.Delivering),
            cancellationToken);
    }

    private async Task<RiderPabiliOrderDetail?> LoadOrderAsync(Guid orderId, Guid riderId, CancellationToken cancellationToken)
    {
        var order = await db.PabiliOrders.AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(i => i.Addons)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.RiderId == riderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        return new RiderPabiliOrderDetail(
            order.Id,
            order.Reference,
            order.Status.ToString(),
            order.MerchantName,
            order.CustomerName,
            order.CustomerPhone,
            order.PickupAddress,
            order.PickupLat,
            order.PickupLng,
            order.DropoffAddress,
            order.DropoffLat,
            order.DropoffLng,
            order.GoodsSubtotal,
            order.DeliveryFee,
            order.AdjustmentAmount,
            order.AdjustmentLabel,
            order.CustomerTotal,
            order.Items.OrderBy(x => x.SortOrder).Select(i => new CustomerPabiliOrderLineItem(
                i.Id,
                i.Name,
                i.Quantity,
                i.UnitSellingPrice,
                i.LineSellingTotal,
                i.Addons.Select(a => new CustomerPabiliOrderAddonItem(
                    a.Id,
                    a.Name,
                    a.Quantity,
                    a.UnitSellingPrice,
                    a.LineSellingTotal)).ToList())).ToList());
    }
}
