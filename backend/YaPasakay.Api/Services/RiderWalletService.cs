using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class RiderWalletService(AppDbContext db)
{
    private static readonly PaymentMethod[] WalletPaymentMethods =
    [
        PaymentMethod.Cash,
        PaymentMethod.GCash,
        PaymentMethod.Maya,
        PaymentMethod.Other
    ];

    public async Task<RiderWallet> EnsureWalletAsync(RiderProfile rider, CancellationToken cancellationToken)
    {
        if (rider.Wallet is not null)
        {
            return rider.Wallet;
        }

        var existing = await db.RiderWallets.FirstOrDefaultAsync(x => x.RiderId == rider.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var wallet = new RiderWallet
        {
            RiderId = rider.Id,
            Balance = 0
        };
        db.RiderWallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    public Task<(RiderWalletTransaction? Transaction, string? Error)> RequestCashInAsync(
        RiderProfile rider,
        decimal amount,
        PaymentMethod paymentMethod,
        string? note,
        CancellationToken cancellationToken,
        Guid? approvedByUserId = null,
        bool requireAcceptedMethod = true) =>
        CreateRequestAsync(
            rider,
            WalletTransactionKind.CashIn,
            amount,
            paymentMethod,
            note,
            approvedByUserId,
            requireAcceptedMethod,
            cancellationToken);

    public Task<(RiderWalletTransaction? Transaction, string? Error)> RequestCashOutAsync(
        RiderProfile rider,
        decimal amount,
        PaymentMethod paymentMethod,
        string? note,
        CancellationToken cancellationToken,
        Guid? approvedByUserId = null,
        bool requireAcceptedMethod = true) =>
        CreateRequestAsync(
            rider,
            WalletTransactionKind.CashOut,
            amount,
            paymentMethod,
            note,
            approvedByUserId,
            requireAcceptedMethod,
            cancellationToken);

    private async Task<(RiderWalletTransaction? Transaction, string? Error)> CreateRequestAsync(
        RiderProfile rider,
        WalletTransactionKind kind,
        decimal amount,
        PaymentMethod paymentMethod,
        string? note,
        Guid? approvedByUserId,
        bool requireAcceptedMethod,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateWalletRequestAsync(rider, amount, paymentMethod, requireAcceptedMethod, cancellationToken);
        if (validation is not null)
        {
            return (null, validation);
        }

        var wallet = await EnsureWalletAsync(rider, cancellationToken);
        var rounded = CommissionCut.Round(amount);
        if (kind == WalletTransactionKind.CashOut && wallet.Balance < rounded)
        {
            return (null, "Insufficient wallet balance for this cash-out request.");
        }

        var approved = approvedByUserId.HasValue;
        if (approved)
        {
            var delta = kind == WalletTransactionKind.CashIn ? rounded : -rounded;
            wallet.Balance = CommissionCut.Round(wallet.Balance + delta);
            wallet.UpdatedAtUtc = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        var tx = new RiderWalletTransaction
        {
            WalletId = wallet.Id,
            RiderId = rider.Id,
            Kind = kind,
            Status = approved ? WalletTransactionStatus.Approved : WalletTransactionStatus.Pending,
            PaymentMethod = paymentMethod,
            Amount = rounded,
            BalanceAfter = approved ? wallet.Balance : null,
            Note = TrimNote(note),
            ResolvedByUserId = approvedByUserId,
            ResolvedAtUtc = approved ? now : null
        };
        db.RiderWalletTransactions.Add(tx);
        await db.SaveChangesAsync(cancellationToken);
        return (tx, null);
    }

    public async Task<(RiderWalletTransaction? Transaction, string? Error)> ApproveAsync(
        Guid transactionId,
        Guid operatorUserId,
        CancellationToken cancellationToken)
    {
        var tx = await db.RiderWalletTransactions
            .Include(x => x.Wallet)
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (tx is null)
        {
            return (null, "Wallet request not found.");
        }

        if (tx.Status != WalletTransactionStatus.Pending)
        {
            return (null, "This wallet request was already processed.");
        }

        if (tx.Kind is WalletTransactionKind.CashIn or WalletTransactionKind.CashOut)
        {
            var delta = tx.Kind == WalletTransactionKind.CashIn ? tx.Amount : -tx.Amount;
            if (tx.Kind == WalletTransactionKind.CashOut && tx.Wallet.Balance + delta < 0)
            {
                return (null, "Rider no longer has enough balance for this cash-out.");
            }

            tx.Wallet.Balance = CommissionCut.Round(tx.Wallet.Balance + delta);
            tx.Wallet.UpdatedAtUtc = DateTime.UtcNow;
            tx.BalanceAfter = tx.Wallet.Balance;
        }

        tx.Status = WalletTransactionStatus.Approved;
        tx.ResolvedAtUtc = DateTime.UtcNow;
        tx.ResolvedByUserId = operatorUserId;
        tx.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (tx, null);
    }

    public async Task<(RiderWalletTransaction? Transaction, string? Error)> RejectAsync(
        Guid transactionId,
        Guid operatorUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var tx = await db.RiderWalletTransactions
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (tx is null)
        {
            return (null, "Wallet request not found.");
        }

        if (tx.Status != WalletTransactionStatus.Pending)
        {
            return (null, "This wallet request was already processed.");
        }

        if (tx.Kind == WalletTransactionKind.Commission)
        {
            return (null, "Commission deductions cannot be rejected.");
        }

        tx.Status = WalletTransactionStatus.Rejected;
        tx.RejectionReason = TrimReason(reason) ?? "Rejected by operator.";
        tx.ResolvedAtUtc = DateTime.UtcNow;
        tx.ResolvedByUserId = operatorUserId;
        tx.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (tx, null);
    }

    public async Task<(RiderWalletTransaction? Transaction, string? Error)> ApplyCommissionAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.Status != TripStatus.Completed)
        {
            return (null, "Commission applies only to completed trips.");
        }

        if (await db.RiderWalletTransactions.AnyAsync(
                x => x.TripId == trip.Id && x.Kind == WalletTransactionKind.Commission,
                cancellationToken))
        {
            return (null, "Commission was already deducted for this trip.");
        }

        var fare = await db.FareMatrices
            .FirstOrDefaultAsync(
                x => x.OperatorId == trip.OperatorId && x.VehicleType == trip.VehicleType && x.IsActive,
                cancellationToken);
        var operatorPercent = fare?.OperatorCommissionPercent ?? FareCommissionSplit.DefaultOperatorShare;
        var amount = CommissionCut.Round(trip.Fare * operatorPercent / 100m);
        if (amount <= 0)
        {
            return (null, null);
        }

        var rider = await db.RiderProfiles
            .Include(x => x.Wallet)
            .FirstAsync(x => x.Id == trip.RiderId, cancellationToken);
        var wallet = await EnsureWalletAsync(rider, cancellationToken);
        wallet.Balance = CommissionCut.Round(wallet.Balance - amount);
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        var tx = new RiderWalletTransaction
        {
            WalletId = wallet.Id,
            RiderId = rider.Id,
            Kind = WalletTransactionKind.Commission,
            Status = WalletTransactionStatus.Approved,
            Amount = amount,
            BalanceAfter = wallet.Balance,
            TripId = trip.Id,
            Note = $"Operator commission ({operatorPercent:0.##}%) for {trip.Reference}",
            ResolvedAtUtc = DateTime.UtcNow
        };
        db.RiderWalletTransactions.Add(tx);
        await db.SaveChangesAsync(cancellationToken);
        return (tx, null);
    }

    public static WalletTransactionItem Map(RiderWalletTransaction tx, string? tripReference = null, decimal? tripFare = null) =>
        new(
            tx.Id,
            tx.Kind,
            tx.Status,
            tx.PaymentMethod,
            tx.Amount,
            tx.BalanceAfter,
            tx.TripId,
            tripReference,
            tripFare,
            tx.Note,
            tx.RejectionReason,
            DateTime.SpecifyKind(tx.CreatedAtUtc, DateTimeKind.Utc),
            tx.ResolvedAtUtc is DateTime resolved ? DateTime.SpecifyKind(resolved, DateTimeKind.Utc) : null);

    private async Task<string?> ValidateWalletRequestAsync(
        RiderProfile rider,
        decimal amount,
        PaymentMethod paymentMethod,
        bool requireAcceptedMethod,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return "Enter an amount greater than zero.";
        }

        if (!WalletPaymentMethods.Contains(paymentMethod))
        {
            return "Choose CASH, GCASH, MAYA, or OTHERS.";
        }

        if (!requireAcceptedMethod)
        {
            return null;
        }

        var accepted = await db.RiderPaymentMethods
            .Where(x => x.RiderId == rider.Id)
            .Select(x => x.Method)
            .ToListAsync(cancellationToken);
        if (!accepted.Contains(paymentMethod))
        {
            return "This rider does not accept that payment method.";
        }

        return null;
    }

    private static string? TrimNote(string? note)
    {
        var trimmed = (note ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed[..Math.Min(trimmed.Length, 200)];
    }

    private static string? TrimReason(string? reason)
    {
        var trimmed = (reason ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed[..Math.Min(trimmed.Length, 200)];
    }
}
