using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/pabili-orders")]
public class OperatorPabiliOrdersController(AppDbContext db, LiveNotify live) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PabiliOrders.AsNoTracking()
            .Include(x => x.Rider)!.ThenInclude(r => r!.AppUser)
            .Where(x => x.OperatorId == op.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Reference.Contains(term)
                || x.CustomerName.Contains(term)
                || x.CustomerPhone.Contains(term)
                || x.MerchantName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PabiliOrderStatus>(status, true, out var parsed))
        {
            query = query.Where(x => x.Status == parsed);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total,
            page,
            pageSize,
            items = rows.Select(x => new OperatorPabiliOrderListItem(
                x.Id,
                x.Reference,
                x.Status.ToString(),
                x.MerchantName,
                x.CustomerName,
                x.CustomerPhone,
                x.CustomerTotal,
                x.AdjustmentAmount,
                x.Rider?.AppUser.FullName,
                DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc))).ToList()
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperatorPabiliOrderDetail>> Detail(Guid id, CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        var detail = await LoadDetailAsync(id, op.Id, cancellationToken);
        return detail is null ? NotFound(new { message = "Order not found." }) : Ok(detail);
    }

    [HttpPatch("{id:guid}/adjustment")]
    public async Task<ActionResult<OperatorPabiliOrderDetail>> PatchAdjustment(
        Guid id,
        [FromBody] PatchPabiliAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        var order = await db.PabiliOrders.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (order.Status is PabiliOrderStatus.Completed or PabiliOrderStatus.Cancelled)
        {
            return BadRequest(new { message = "Adjustment is locked after the order is completed or cancelled." });
        }

        var label = (request.AdjustmentLabel ?? string.Empty).Trim();
        if (request.AdjustmentAmount != 0 && string.IsNullOrWhiteSpace(label))
        {
            return BadRequest(new { message = "Provide a customer-visible label when adjustment is not zero." });
        }

        var total = PabiliPricingService.CustomerTotal(order.GoodsSubtotal, order.DeliveryFee, request.AdjustmentAmount);
        if (total < 0)
        {
            return BadRequest(new { message = "Customer total cannot be negative." });
        }

        order.AdjustmentAmount = CommissionCut.Round(request.AdjustmentAmount);
        order.AdjustmentLabel = request.AdjustmentAmount == 0 ? string.Empty : label;
        order.CustomerTotal = total;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await live.CustomerChangedAsync(order.CustomerId, "pabili-adjustment", cancellationToken);
        if (order.RiderId is Guid riderId)
        {
            await live.RiderChangedAsync(riderId, "pabili-adjustment", cancellationToken);
        }

        return Ok(await LoadDetailAsync(order.Id, op.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OperatorPabiliOrderDetail>> Cancel(
        Guid id,
        [FromBody] CancelPabiliOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        var order = await db.PabiliOrders
            .Include(x => x.Offers)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (order.Status is PabiliOrderStatus.Completed or PabiliOrderStatus.Cancelled)
        {
            return BadRequest(new { message = "Order is already closed." });
        }

        order.Status = PabiliOrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
        order.CancelledBy = CancelledBy.Operator;
        order.CancelReason = string.IsNullOrWhiteSpace(request?.Reason) ? "Cancelled by operator." : request!.Reason!.Trim();
        order.UpdatedAtUtc = DateTime.UtcNow;
        foreach (var offer in order.Offers.Where(x => x.Status == OfferStatus.Offered))
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await live.CustomerChangedAsync(order.CustomerId, "pabili-cancelled", cancellationToken);
        if (order.RiderId is Guid riderId)
        {
            await live.RiderChangedAsync(riderId, "pabili-cancelled", cancellationToken);
        }

        return Ok(await LoadDetailAsync(order.Id, op.Id, cancellationToken));
    }

    private async Task<OperatorPabiliOrderDetail?> LoadDetailAsync(Guid id, Guid operatorId, CancellationToken cancellationToken)
    {
        var order = await db.PabiliOrders.AsNoTracking()
            .Include(x => x.Rider)!.ThenInclude(r => r!.AppUser)
            .Include(x => x.Items)
                .ThenInclude(i => i.Addons)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var canAdjust = order.Status is PabiliOrderStatus.Pending
            or PabiliOrderStatus.Waiting
            or PabiliOrderStatus.PickedUp
            or PabiliOrderStatus.Delivering;
        var canCancel = order.Status is not (PabiliOrderStatus.Completed or PabiliOrderStatus.Cancelled);

        return new OperatorPabiliOrderDetail(
            order.Id,
            order.Reference,
            order.Status.ToString(),
            order.MerchantId,
            order.MerchantName,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.PickupAddress,
            order.PickupLat,
            order.PickupLng,
            order.DropoffAddress,
            order.DropoffLat,
            order.DropoffLng,
            order.DistanceKm,
            order.GoodsSubtotal,
            order.GoodsBaseSubtotal,
            order.DeliveryFee,
            order.SurchargeTotal,
            order.AdjustmentAmount,
            order.AdjustmentLabel,
            order.CustomerTotal,
            order.PaymentMethod.ToString(),
            order.RiderId,
            order.Rider?.AppUser.FullName,
            order.Rider?.AppUser.PhoneNumber,
            order.Notes,
            order.CancelReason,
            DateTime.SpecifyKind(order.CreatedAtUtc, DateTimeKind.Utc),
            order.AcceptedAtUtc,
            order.PickedUpAtUtc,
            order.DeliveringAtUtc,
            order.CompletedAtUtc,
            order.CancelledAtUtc,
            canAdjust,
            canCancel,
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
