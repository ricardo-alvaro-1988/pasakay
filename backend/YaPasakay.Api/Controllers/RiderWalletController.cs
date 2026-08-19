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
[Route("api/rider/wallet")]
public class RiderWalletController(AppDbContext db, RiderWalletService wallets) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RiderWalletResponse>> Get(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await BuildSummaryAsync(rider.Id, rider.AppUser.FullName, cancellationToken));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionItem>>> Transactions(CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await LoadTransactionsAsync(rider.Id, cancellationToken);
        return Ok(rows);
    }

    [HttpPost("cash-in")]
    public async Task<ActionResult<WalletTransactionItem>> CashIn(
        [FromBody] CreateWalletRequestBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var (tx, error) = await wallets.RequestCashInAsync(
            rider,
            request.Amount,
            request.PaymentMethod,
            request.Note,
            cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(RiderWalletService.Map(tx!));
    }

    [HttpPost("cash-out")]
    public async Task<ActionResult<WalletTransactionItem>> CashOut(
        [FromBody] CreateWalletRequestBody request,
        CancellationToken cancellationToken)
    {
        var (rider, status, message) = await RiderContext.RequireAsync(db, User, cancellationToken);
        if (rider is null)
        {
            return StatusCode(status, new { message });
        }

        var (tx, error) = await wallets.RequestCashOutAsync(
            rider,
            request.Amount,
            request.PaymentMethod,
            request.Note,
            cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(RiderWalletService.Map(tx!));
    }

    private async Task<RiderWalletResponse> BuildSummaryAsync(
        Guid riderId,
        string riderName,
        CancellationToken cancellationToken)
    {
        var wallet = await db.RiderWallets.FirstOrDefaultAsync(x => x.RiderId == riderId, cancellationToken);
        var balance = wallet?.Balance ?? 0;
        var pending = await db.RiderWalletTransactions.CountAsync(
            x => x.RiderId == riderId && x.Status == WalletTransactionStatus.Pending,
            cancellationToken);
        var recent = await LoadTransactionsAsync(riderId, cancellationToken, 20);
        return new RiderWalletResponse(riderId, riderName, balance, pending, recent);
    }

    private async Task<IReadOnlyList<WalletTransactionItem>> LoadTransactionsAsync(
        Guid riderId,
        CancellationToken cancellationToken,
        int take = 100)
    {
        var rows = await db.RiderWalletTransactions
            .Include(x => x.Trip)
            .Where(x => x.RiderId == riderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(x => RiderWalletService.Map(x, x.Trip?.Reference, x.Trip?.Fare)).ToList();
    }
}
