using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain;
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
                PendingOtherFare = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null
                        && t.VehicleType != VehicleType.Motorcycle && t.VehicleType != VehicleType.Tricycle)
                    .Sum(t => (decimal?)t.Fare) ?? 0,
                OldestUnbilledUtc = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null)
                    .Min(t => t.CompletedAtUtc),
                NewestUnbilledUtc = db.Trips
                    .Where(t => t.OperatorId == op.Id && t.Status == TripStatus.Completed && t.BillId == null)
                    .Max(t => t.CompletedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var operatorIds = rows.Select(x => x.Id).ToList();
        var offers = await db.OperatorVehicleOffers
            .AsNoTracking()
            .Include(x => x.VehicleCategory)
            .Where(x => operatorIds.Contains(x.OperatorId))
            .ToListAsync(cancellationToken);
        var offersByOp = offers.GroupBy(x => x.OperatorId).ToDictionary(g => g.Key, g => g.ToList());
        var pendingTrips = operatorIds.Count == 0
            ? []
            : await db.Trips
                .Where(t => operatorIds.Contains(t.OperatorId) && t.Status == TripStatus.Completed && t.BillId == null)
                .Select(t => new { t.OperatorId, t.VehicleType, t.VehicleCategoryId, t.Fare })
                .ToListAsync(cancellationToken);
        var pendingByOp = pendingTrips.GroupBy(t => t.OperatorId).ToDictionary(g => g.Key, g => g.ToList());

        var categoryIds = pendingTrips
            .Select(t => ResolveCategoryId(t.VehicleCategoryId, t.VehicleType))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var categoryMeta = await LoadCategoryMetaAsync(categoryIds, cancellationToken);
        foreach (var offer in offers)
        {
            if (offer.VehicleCategory is { } cat)
            {
                categoryMeta[cat.Id] = (cat.Code, cat.Name);
            }
        }

        var items = rows
            .Select(op =>
            {
                var opOffers = offersByOp.GetValueOrDefault(op.Id) ?? [];
                var opStub = new Operator
                {
                    MotorcycleCommissionPercent = op.MotorcycleCommissionPercent,
                    TricycleCommissionPercent = op.TricycleCommissionPercent,
                    VehicleOffers = opOffers
                };
                var trips = pendingByOp.GetValueOrDefault(op.Id) ?? [];
                var pendingLines = BuildPendingVehicleLines(
                    trips.Select(t => (t.VehicleType, t.VehicleCategoryId, t.Fare)),
                    opStub,
                    categoryMeta);
                var motorcycle = pendingLines.FirstOrDefault(x => x.VehicleCode == "motorcycle")?.Amount ?? 0;
                var tricycle = pendingLines.FirstOrDefault(x => x.VehicleCode == "tricycle")?.Amount ?? 0;
                var pendingCommission = CommissionCut.Round(pendingLines.Sum(x => x.Amount));
                return new BillingOperatorListItem(
                    op.Id,
                    op.CompanyName,
                    op.ContactName,
                    op.ContactPhone,
                    UploadUrls.FromPath(op.ProfilePhotoPath),
                    op.IsActive,
                    op.MotorcycleCommissionPercent,
                    op.TricycleCommissionPercent,
                    pendingCommission,
                    motorcycle,
                    tricycle,
                    op.PendingTripCount,
                    op.OldestUnbilledUtc,
                    op.NewestUnbilledUtc,
                    pendingLines);
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
            .Include(x => x.VehicleOffers).ThenInclude(x => x.VehicleCategory)
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
            .Include(x => x.VehicleCategory)
            .Where(x => x.OperatorId == operatorId && x.Status == TripStatus.Completed && x.BillId == null)
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return BadRequest(new { message = "This Operator has no pending commission to bill." });
        }

        var categoryMeta = trips
            .Where(t => t.VehicleCategory is not null)
            .GroupBy(t => t.VehicleCategory!.Id)
            .ToDictionary(g => g.Key, g => (g.First().VehicleCategory!.Code, g.First().VehicleCategory!.Name));
        foreach (var offer in op.VehicleOffers)
        {
            if (offer.VehicleCategory is { } cat)
            {
                categoryMeta[cat.Id] = (cat.Code, cat.Name);
            }
        }

        var missingIds = trips
            .Select(t => ResolveCategoryId(t.VehicleCategoryId, t.VehicleType))
            .Where(id => id.HasValue && !categoryMeta.ContainsKey(id!.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (missingIds.Count > 0)
        {
            foreach (var (id, meta) in await LoadCategoryMetaAsync(missingIds, cancellationToken))
            {
                categoryMeta[id] = meta;
            }
        }

        var lines = trips
            .GroupBy(x => ResolveCategoryId(x.VehicleCategoryId, x.VehicleType))
            .Select(g =>
            {
                var sample = g.First();
                var categoryId = g.Key;
                var amount = CommissionCut.Round(g.Sum(t =>
                    CommissionCut.Of(t.Fare, FareCommissionSplit.SystemPercent(op, categoryId, t.VehicleType))));
                var (code, name) = ResolveLineLabel(categoryId, sample.VehicleType, categoryMeta);
                return new
                {
                    CategoryId = categoryId,
                    Code = code,
                    Name = name,
                    Amount = amount,
                };
            })
            .Where(x => x.Amount > 0)
            .ToList();

        var motorcycle = lines.FirstOrDefault(x => x.Code == "motorcycle")?.Amount ?? 0;
        var tricycle = lines.FirstOrDefault(x => x.Code == "tricycle")?.Amount ?? 0;
        var amount = CommissionCut.Round(lines.Sum(x => x.Amount));
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

        foreach (var line in lines)
        {
            bill.VehicleLines.Add(new OperatorBillVehicleLine
            {
                VehicleCategoryId = line.CategoryId,
                VehicleCode = line.Code,
                VehicleName = line.Name,
                Amount = line.Amount,
            });
        }

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
        var op = await db.Operators
            .Include(x => x.VehicleOffers).ThenInclude(x => x.VehicleCategory)
            .FirstOrDefaultAsync(x => x.Id == operatorId, cancellationToken);
        if (op is null)
        {
            return null;
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == operatorId && x.Status == TripStatus.Completed && x.BillId == null)
            .Select(x => new { x.VehicleType, x.VehicleCategoryId, x.Fare, x.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        var categoryIds = trips
            .Select(t => ResolveCategoryId(t.VehicleCategoryId, t.VehicleType))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var categoryMeta = await LoadCategoryMetaAsync(categoryIds, cancellationToken);
        foreach (var offer in op.VehicleOffers)
        {
            if (offer.VehicleCategory is { } cat)
            {
                categoryMeta[cat.Id] = (cat.Code, cat.Name);
            }
        }

        var pendingLines = BuildPendingVehicleLines(
            trips.Select(t => (t.VehicleType, t.VehicleCategoryId, t.Fare)),
            op,
            categoryMeta);

        var motorcycle = pendingLines.FirstOrDefault(x => x.VehicleCode == "motorcycle")?.Amount ?? 0;
        var tricycle = pendingLines.FirstOrDefault(x => x.VehicleCode == "tricycle")?.Amount ?? 0;
        var pendingCommission = CommissionCut.Round(pendingLines.Sum(x => x.Amount));

        var billRows = await db.OperatorBills
            .Include(x => x.VehicleLines)
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
                    x.VehicleType,
                    x.VehicleCategoryId
                })
                .ToListAsync(cancellationToken);

        var bills = billRows.Select(x =>
        {
            var tripLines = billedTrips
                .Where(t => t.BillId == x.Id)
                .OrderBy(t => t.AtUtc)
                .Select(t => new BillTripItem(
                    DateTime.SpecifyKind(t.AtUtc, DateTimeKind.Utc),
                    t.RiderName,
                    t.Reference,
                    t.Fare,
                    CommissionCut.Round(CommissionCut.Of(
                        t.Fare,
                        FareCommissionSplit.SystemPercent(op, t.VehicleCategoryId, t.VehicleType)))))
                .ToList();
            var vehicleLines = x.VehicleLines
                .Select(l => new OperatorBillVehicleLineItem(l.VehicleCode, l.VehicleName, l.Amount))
                .ToList();
            if (vehicleLines.Count == 0)
            {
                if (x.MotorcycleAmount > 0)
                {
                    vehicleLines.Add(new OperatorBillVehicleLineItem("motorcycle", "Motorcycle", x.MotorcycleAmount));
                }

                if (x.TricycleAmount > 0)
                {
                    vehicleLines.Add(new OperatorBillVehicleLineItem("tricycle", "Tricycle", x.TricycleAmount));
                }
            }

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
                tripLines,
                vehicleLines);
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
            pendingCommission,
            motorcycle,
            tricycle,
            trips.Count,
            trips.Count == 0 ? null : trips.Min(x => x.CompletedAtUtc),
            trips.Count == 0 ? null : trips.Max(x => x.CompletedAtUtc),
            bills,
            pendingLines);
    }

    private async Task<Dictionary<Guid, (string Code, string Name)>> LoadCategoryMetaAsync(
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            return new Dictionary<Guid, (string Code, string Name)>();
        }

        return await db.VehicleCategories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => (x.Code, x.Name), cancellationToken);
    }

    private static Guid? ResolveCategoryId(Guid? vehicleCategoryId, VehicleType vehicleType) =>
        vehicleCategoryId ?? VehicleCatalog.PresetFor(vehicleType)?.Id;

    private static (string Code, string Name) ResolveLineLabel(
        Guid? categoryId,
        VehicleType vehicleType,
        IReadOnlyDictionary<Guid, (string Code, string Name)> categoryMeta)
    {
        if (categoryId is Guid id)
        {
            if (categoryMeta.TryGetValue(id, out var meta))
            {
                return meta;
            }

            var byId = VehicleCatalog.PresetFor(id);
            if (byId is not null)
            {
                return (byId.Code, byId.Name);
            }
        }

        var byType = VehicleCatalog.PresetFor(vehicleType);
        if (byType is not null)
        {
            return (byType.Code, byType.Name);
        }

        return ("custom", "Custom");
    }

    private static string NextBillNumber() =>
        $"BILL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static IReadOnlyList<OperatorBillVehicleLineItem> BuildPendingVehicleLines(
        IEnumerable<(VehicleType VehicleType, Guid? VehicleCategoryId, decimal Fare)> trips,
        Operator op,
        IReadOnlyDictionary<Guid, (string Code, string Name)> categoryMeta) =>
        trips
            .GroupBy(x => ResolveCategoryId(x.VehicleCategoryId, x.VehicleType))
            .Select(g =>
            {
                var sampleType = g.First().VehicleType;
                var categoryId = g.Key;
                var amount = CommissionCut.Round(g.Sum(t =>
                    CommissionCut.Of(t.Fare, FareCommissionSplit.SystemPercent(op, categoryId, t.VehicleType))));
                var (code, name) = ResolveLineLabel(categoryId, sampleType, categoryMeta);
                return new OperatorBillVehicleLineItem(code, name, amount);
            })
            .Where(x => x.Amount > 0)
            .OrderBy(x => x.VehicleName)
            .ToList();
}
