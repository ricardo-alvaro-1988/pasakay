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
[Route("api/operator/wallet")]
public class OperatorWalletController(AppDbContext db, RiderWalletService wallets, LiveNotify live) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorWalletOverviewResponse>> Overview(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var riders = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Wallet)
            .Where(x => x.OperatorId == op!.Id)
            .OrderBy(x => x.AppUser.FullName)
            .ToListAsync(cancellationToken);

        var pendingCounts = await db.RiderWalletTransactions
            .Where(x =>
                x.Rider.OperatorId == op.Id
                && x.Status == WalletTransactionStatus.Pending
                && (x.Kind == WalletTransactionKind.CashIn || x.Kind == WalletTransactionKind.CashOut))
            .GroupBy(x => x.RiderId)
            .Select(g => new { RiderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RiderId, x => x.Count, cancellationToken);

        var items = riders.Select(rider =>
        {
            pendingCounts.TryGetValue(rider.Id, out var pending);
            return new RiderWalletBalanceItem(
                rider.Id,
                rider.AppUser.FullName,
                rider.AppUser.PhoneNumber,
                rider.PlateNumber,
                rider.VehicleType,
                rider.IsActive,
                rider.Wallet?.Balance ?? 0,
                pending);
        }).ToList();

        var pendingRequests = pendingCounts.Values.Sum();
        return Ok(new OperatorWalletOverviewResponse(
            items.Sum(x => x.Balance),
            pendingRequests,
            items));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<WalletRequestItem>>> PendingRequests(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.RiderWalletTransactions
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Where(x =>
                x.Rider.OperatorId == op!.Id
                && x.Status == WalletTransactionStatus.Pending
                && (x.Kind == WalletTransactionKind.CashIn || x.Kind == WalletTransactionKind.CashOut))
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(MapRequest).ToList());
    }

    [HttpGet("history")]
    public async Task<ActionResult<PagedResult<WalletHistoryItem>>> History(
        [FromQuery] string? q,
        [FromQuery] WalletTransactionKind? kind,
        [FromQuery] Guid? riderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.RiderWalletTransactions
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Include(x => x.Trip)
            .Where(x => x.Rider.OperatorId == op!.Id);

        if (riderId.HasValue)
        {
            query = query.Where(x => x.RiderId == riderId.Value);
        }

        if (kind.HasValue)
        {
            query = query.Where(x => x.Kind == kind.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Rider.AppUser.FullName.Contains(term) ||
                x.Rider.AppUser.PhoneNumber.Contains(term) ||
                x.Rider.PlateNumber.Contains(term) ||
                (x.Trip != null && x.Trip.Reference.Contains(term)) ||
                (x.Note != null && x.Note.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<WalletHistoryItem>(
            rows.Select(MapHistory).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("riders/{riderId:guid}")]
    public async Task<ActionResult<RiderWalletDetailResponse>> RiderWallet(
        Guid riderId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.Wallet)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == riderId, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        await wallets.EnsureWalletAsync(rider, cancellationToken);
        var wallet = await db.RiderWallets.FirstAsync(x => x.RiderId == riderId, cancellationToken);
        var pending = await db.RiderWalletTransactions.CountAsync(
            x => x.RiderId == riderId && x.Status == WalletTransactionStatus.Pending,
            cancellationToken);
        var transactions = await db.RiderWalletTransactions
            .Include(x => x.Trip)
            .Where(x => x.RiderId == riderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(new RiderWalletDetailResponse(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            wallet.Balance,
            pending,
            transactions.Select(x => RiderWalletService.Map(x, x.Trip?.Reference)).ToList()));
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<ActionResult<ResolveWalletRequestResult>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var userId = AdminAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await db.RiderWalletTransactions.AnyAsync(
                x => x.Id == id && x.Rider.OperatorId == op!.Id,
                cancellationToken))
        {
            return NotFound();
        }

        var (tx, error) = await wallets.ApproveAsync(id, userId.Value, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var tripRef = tx!.TripId is Guid tripId
            ? await db.Trips.Where(x => x.Id == tripId).Select(x => x.Reference).FirstOrDefaultAsync(cancellationToken)
            : null;
        var balance = await db.RiderWallets.Where(x => x.RiderId == tx.RiderId).Select(x => x.Balance).FirstAsync(cancellationToken);
        await live.RiderChangedAsync(tx.RiderId, "wallet", cancellationToken);
        return Ok(new ResolveWalletRequestResult(RiderWalletService.Map(tx, tripRef), balance));
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<ActionResult<ResolveWalletRequestResult>> Reject(
        Guid id,
        [FromBody] RejectWalletRequestBody request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var userId = AdminAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await db.RiderWalletTransactions.AnyAsync(
                x => x.Id == id && x.Rider.OperatorId == op!.Id,
                cancellationToken))
        {
            return NotFound();
        }

        var (tx, error) = await wallets.RejectAsync(id, userId.Value, request.Reason, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var balance = await db.RiderWallets.Where(x => x.RiderId == tx!.RiderId).Select(x => x.Balance).FirstOrDefaultAsync(cancellationToken);
        await live.RiderChangedAsync(tx!.RiderId, "wallet", cancellationToken);
        return Ok(new ResolveWalletRequestResult(RiderWalletService.Map(tx!), balance));
    }

    [HttpPost("riders/{riderId:guid}/cash-in")]
    public async Task<ActionResult<WalletTransactionItem>> RiderCashIn(
        Guid riderId,
        [FromBody] CreateOperatorWalletRequestBody request,
        CancellationToken cancellationToken)
    {
        var rider = await LoadRiderAsync(riderId, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        var approvedBy = ApprovedByUserId(request.Approved);
        if (approvedBy.error is not null)
        {
            return Unauthorized();
        }

        var (tx, error) = await wallets.RequestCashInAsync(
            rider,
            request.Amount,
            request.PaymentMethod,
            request.Note,
            cancellationToken,
            approvedBy.userId,
            requireAcceptedMethod: false);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(RiderWalletService.Map(tx!));
    }

    [HttpPost("riders/{riderId:guid}/cash-out")]
    public async Task<ActionResult<WalletTransactionItem>> RiderCashOut(
        Guid riderId,
        [FromBody] CreateOperatorWalletRequestBody request,
        CancellationToken cancellationToken)
    {
        var rider = await LoadRiderAsync(riderId, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        var approvedBy = ApprovedByUserId(request.Approved);
        if (approvedBy.error is not null)
        {
            return Unauthorized();
        }

        var (tx, error) = await wallets.RequestCashOutAsync(
            rider,
            request.Amount,
            request.PaymentMethod,
            request.Note,
            cancellationToken,
            approvedBy.userId,
            requireAcceptedMethod: false);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(RiderWalletService.Map(tx!));
    }

    private (Guid? userId, string? error) ApprovedByUserId(bool approved)
    {
        if (!approved)
        {
            return (null, null);
        }

        var userId = AdminAccess.UserId(User);
        return userId is null ? (null, "Unauthorized") : (userId, null);
    }

    private async Task<YaPasakay.Domain.Entities.RiderProfile?> LoadRiderAsync(
        Guid riderId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return null;
        }

        return await db.RiderProfiles
            .Include(x => x.PaymentMethods)
            .FirstOrDefaultAsync(x => x.OperatorId == op.Id && x.Id == riderId, cancellationToken);
    }

    private static WalletRequestItem MapRequest(YaPasakay.Domain.Entities.RiderWalletTransaction tx) =>
        new(
            tx.Id,
            tx.RiderId,
            tx.Rider.AppUser.FullName,
            tx.Rider.AppUser.PhoneNumber,
            tx.Rider.PlateNumber,
            tx.Kind,
            tx.PaymentMethod ?? PaymentMethod.Cash,
            tx.Amount,
            tx.Note,
            DateTime.SpecifyKind(tx.CreatedAtUtc, DateTimeKind.Utc));

    private static WalletHistoryItem MapHistory(YaPasakay.Domain.Entities.RiderWalletTransaction tx) =>
        new(
            tx.Id,
            tx.RiderId,
            tx.Rider.AppUser.FullName,
            tx.Rider.AppUser.PhoneNumber,
            tx.Rider.PlateNumber,
            tx.Kind,
            tx.Status,
            tx.PaymentMethod,
            tx.Amount,
            tx.BalanceAfter,
            tx.TripId,
            tx.Trip?.Reference,
            tx.Note,
            tx.RejectionReason,
            DateTime.SpecifyKind(tx.CreatedAtUtc, DateTimeKind.Utc),
            tx.ResolvedAtUtc is DateTime resolved ? DateTime.SpecifyKind(resolved, DateTimeKind.Utc) : null);
}
