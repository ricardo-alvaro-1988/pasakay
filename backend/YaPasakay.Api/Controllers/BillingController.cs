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
[Authorize(Roles = "Admin")]
[Route("api/admin/billing")]
public class BillingController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<BillingOperatorListItem>>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Operators.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.CompanyName.Contains(term) || x.ContactName.Contains(term) || x.ContactPhone.Contains(term));
        }

        var rows = await query
            .Select(op => new
            {
                op.Id,
                op.CompanyName,
                op.ContactName,
                op.ContactPhone,
                op.ProfilePhotoPath,
                op.IsActive,
                op.MotorcycleCommissionPercent,
                op.TricycleCommissionPercent,
                PendingTripCount = db.Trips.Count(t =>
                    t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null),
                PendingMotorcycleFare = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null && t.VehicleType == VehicleType.Motorcycle)
                    .Sum(t => (decimal?)t.Fare) ?? 0,
                PendingTricycleFare = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null && t.VehicleType == VehicleType.Tricycle)
                    .Sum(t => (decimal?)t.Fare) ?? 0,
                OldestUnbilledUtc = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null)
                    .Min(t => t.CompletedAtUtc),
                NewestUnbilledUtc = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null)
                    .Max(t => t.CompletedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(op =>
            {
                var motorcycle = CommissionCut.Round(op.PendingMotorcycleFare * op.MotorcycleCommissionPercent / 100m);
                var tricycle = CommissionCut.Round(op.PendingTricycleFare * op.TricycleCommissionPercent / 100m);
                return new BillingOperatorListItem(
                    op.Id,
                    op.CompanyName,
                    op.ContactName,
                    op.ContactPhone,
                    UploadUrls.FromPath(op.ProfilePhotoPath),
                    op.IsActive,
                    op.MotorcycleCommissionPercent,
                    op.TricycleCommissionPercent,
                    motorcycle + tricycle,
                    motorcycle,
                    tricycle,
                    op.PendingTripCount,
                    op.OldestUnbilledUtc,
                    op.NewestUnbilledUtc);
            })
            .OrderByDescending(x => x.PendingCommission)
            .ThenBy(x => x.CompanyName)
            .ToList();

        var total = items.Count;
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new PagedResult<BillingOperatorListItem>(pageItems, page, pageSize, total));
    }

    [HttpGet("{operatorId:guid}")]
    public async Task<ActionResult<BillingOperatorDetail>> Get(Guid operatorId, CancellationToken cancellationToken)
    {
        var detail = await MapDetailAsync(operatorId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{operatorId:guid}")]
    public async Task<ActionResult<BillingOperatorDetail>> Create(
        Guid operatorId,
        [FromBody] CreateBillRequest request,
        CancellationToken cancellationToken)
    {
        var op = await db.Operators
            .Include(x => x.Riders)
            .Include(x => x.Users)
            .FirstOrDefaultAsync(x => x.Id == operatorId, cancellationToken);
        if (op is null)
        {
            return NotFound();
        }

        if (!op.IsActive)
        {
            return BadRequest(new { message = "Only an active Operator can be billed." });
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == operatorId && x.Status == TripStatus.Completed && x.BillId == null)
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return BadRequest(new { message = "This Operator has no pending commission to bill." });
        }

        var motorcycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Motorcycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));
        var tricycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Tricycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));
        var amount = motorcycle + tricycle;
        if (amount <= 0)
        {
            return BadRequest(new { message = "This Operator has no pending commission to bill." });
        }

        var from = trips.Min(x => x.CompletedAtUtc ?? x.RequestedAtUtc);
        var to = trips.Max(x => x.CompletedAtUtc ?? x.RequestedAtUtc);
        var disable = request.DisableOperator;
        var bill = new OperatorBill
        {
            OperatorId = op.Id,
            Number = NextBillNumber(),
            Amount = amount,
            MotorcycleAmount = motorcycle,
            TricycleAmount = tricycle,
            TripCount = trips.Count,
            PeriodFromUtc = from,
            PeriodToUtc = to,
            DisabledOperator = disable,
            NotifiedAtUtc = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = BillStatus.Issued
        };

        foreach (var trip in trips)
        {
            trip.BillId = bill.Id;
        }

        var body = disable
            ? $"Billing record {bill.Number} for ₱{amount:0.00} covering {trips.Count} completed trip(s) has been issued. Your Operator account and riders were disabled and will not receive bookings."
            : $"Billing record {bill.Number} for ₱{amount:0.00} covering {trips.Count} completed trip(s) has been issued.";

        db.OperatorBills.Add(bill);
        db.OperatorNotifications.Add(new OperatorNotification
        {
            OperatorId = op.Id,
            BillId = bill.Id,
            Kind = NotificationKind.Billing,
            Title = "New billing record",
            Body = body,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (disable)
        {
            op.IsActive = false;
            op.UpdatedAtUtc = DateTime.UtcNow;
            foreach (var rider in op.Riders)
            {
                rider.IsActive = false;
                rider.UpdatedAtUtc = DateTime.UtcNow;
            }

            foreach (var user in op.Users.Where(x => x.Role is UserRole.Operator or UserRole.Rider))
            {
                user.IsActive = false;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        OperatorAudit.Record(
            db,
            User,
            op.Id,
            AuditAction.BillIssued,
            disable
                ? $"Issued billing record {bill.Number} for ₱{amount:0.00} covering {trips.Count} trip(s) and disabled Operator {op.CompanyName}."
                : $"Issued billing record {bill.Number} for ₱{amount:0.00} covering {trips.Count} trip(s) for {op.CompanyName}.");
        await db.SaveChangesAsync(cancellationToken);
        var detail = await MapDetailAsync(operatorId, cancellationToken);
        return Ok(detail);
    }

    private async Task<BillingOperatorDetail?> MapDetailAsync(Guid operatorId, CancellationToken cancellationToken)
    {
        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == operatorId, cancellationToken);
        if (op is null)
        {
            return null;
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == operatorId && x.Status == TripStatus.Completed && x.BillId == null)
            .Select(x => new { x.VehicleType, x.Fare, x.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        var motorcycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Motorcycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));
        var tricycle = CommissionCut.Round(trips
            .Where(x => x.VehicleType == VehicleType.Tricycle)
            .Sum(x => CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));

        var billRows = await db.OperatorBills
            .Where(x => x.OperatorId == operatorId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var billIds = billRows.Select(x => x.Id).ToList();
        var billedTrips = billIds.Count == 0
            ? []
            : await db.Trips
                .Where(x => x.BillId != null && billIds.Contains(x.BillId.Value))
                .Select(x => new
                {
                    x.BillId,
                    AtUtc = x.CompletedAtUtc ?? x.RequestedAtUtc,
                    RiderName = x.Rider.AppUser.FullName,
                    x.Reference,
                    x.Fare,
                    x.VehicleType
                })
                .ToListAsync(cancellationToken);

        var bills = billRows.Select(x =>
        {
            var lines = billedTrips
                .Where(t => t.BillId == x.Id)
                .OrderBy(t => t.AtUtc)
                .Select(t => new BillTripItem(
                    DateTime.SpecifyKind(t.AtUtc, DateTimeKind.Utc),
                    t.RiderName,
                    t.Reference,
                    t.Fare,
                    CommissionCut.Round(CommissionCut.Of(
                        t.Fare,
                        t.VehicleType,
                        op.MotorcycleCommissionPercent,
                        op.TricycleCommissionPercent))))
                .ToList();
            return new BillListItem(
                x.Id,
                x.Number,
                x.Status,
                x.Amount,
                x.MotorcycleAmount,
                x.TricycleAmount,
                x.TripCount,
                x.PeriodFromUtc,
                x.PeriodToUtc,
                x.DisabledOperator,
                x.NotifiedAtUtc,
                x.CreatedAtUtc,
                x.Note,
                lines);
        }).ToList();

        var riderCount = await db.RiderProfiles.CountAsync(x => x.OperatorId == operatorId, cancellationToken);

        return new BillingOperatorDetail(
            op.Id,
            op.CompanyName,
            op.ContactName,
            op.ContactPhone,
            UploadUrls.FromPath(op.ProfilePhotoPath),
            op.IsActive,
            riderCount,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            motorcycle + tricycle,
            motorcycle,
            tricycle,
            trips.Count,
            trips.Count == 0 ? null : trips.Min(x => x.CompletedAtUtc),
            trips.Count == 0 ? null : trips.Max(x => x.CompletedAtUtc),
            bills);
    }

    private static string NextBillNumber() =>
        $"BILL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
