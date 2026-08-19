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
[Route("api/operator")]
public class OperatorController(AppDbContext db, TripBroadcastService broadcast) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<OperatorOverviewResponse>> Overview(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var riders = await db.RiderProfiles.Where(x => x.OperatorId == op!.Id).ToListAsync(cancellationToken);
        var tripsToday = await db.Trips.CountAsync(
            x => x.OperatorId == op.Id && x.Status == TripStatus.Completed && x.CompletedAtUtc >= DateTime.UtcNow.Date,
            cancellationToken);
        var openSos = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var openTickets = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Status == SupportStatus.Open,
            cancellationToken);
        var unread = await db.OperatorNotifications.CountAsync(
            x => x.OperatorId == op.Id && x.ReadAtUtc == null,
            cancellationToken);
        var pendingTrips = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.Status == TripStatus.Completed && x.BillId == null)
            .Select(x => new { x.VehicleType, x.Fare })
            .ToListAsync(cancellationToken);
        var pending = CommissionCut.Round(pendingTrips.Sum(x =>
            CommissionCut.Of(x.Fare, x.VehicleType, op.MotorcycleCommissionPercent, op.TricycleCommissionPercent)));

        var now = DateTime.UtcNow;
        var today = now.Date;
        var from = today.AddDays(-6);
        var window = await db.Trips
            .Where(x => x.OperatorId == op.Id && (x.RequestedAtUtc >= from || (x.CompletedAtUtc != null && x.CompletedAtUtc >= from)))
            .Select(x => new { x.Status, x.Fare, x.RequestedAtUtc, x.CompletedAtUtc, x.ScheduledAtUtc })
            .ToListAsync(cancellationToken);

        bool LivePending(TripStatus status, DateTime? scheduled) =>
            status == TripStatus.Pending && (scheduled is null || scheduled <= now);

        var salesToday = window
            .Where(x => x.Status == TripStatus.Completed && (x.CompletedAtUtc ?? x.RequestedAtUtc).Date == today)
            .Sum(x => x.Fare);
        var pendingNow = await db.Trips.CountAsync(
            x => x.OperatorId == op.Id && x.Status == TripStatus.Pending && (x.ScheduledAtUtc == null || x.ScheduledAtUtc <= now),
            cancellationToken);
        var ongoingNow = await db.Trips.CountAsync(
            x => x.OperatorId == op.Id && x.Status == TripStatus.Ongoing,
            cancellationToken);
        var completeToday = window.Count(x => x.Status == TripStatus.Completed && (x.CompletedAtUtc ?? x.RequestedAtUtc).Date == today);

        var series = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = from.AddDays(offset).Date;
                var sales = window
                    .Where(x => x.Status == TripStatus.Completed && (x.CompletedAtUtc ?? x.RequestedAtUtc).Date == day)
                    .Sum(x => x.Fare);
                var pendingDay = window.Count(x => LivePending(x.Status, x.ScheduledAtUtc) && x.RequestedAtUtc.Date == day);
                var ongoingDay = window.Count(x => x.Status == TripStatus.Ongoing && x.RequestedAtUtc.Date == day);
                var completeDay = window.Count(x => x.Status == TripStatus.Completed && (x.CompletedAtUtc ?? x.RequestedAtUtc).Date == day);
                return new OperatorOverviewSeriesPoint(DateOnly.FromDateTime(day), sales, pendingDay, ongoingDay, completeDay);
            })
            .ToList();

        return Ok(new OperatorOverviewResponse(
            op.CompanyName,
            op.IsActive,
            riders.Count,
            riders.Count(x => x.VehicleType == VehicleType.Motorcycle),
            riders.Count(x => x.VehicleType == VehicleType.Tricycle),
            tripsToday,
            openSos,
            openTickets,
            pending,
            unread,
            salesToday,
            pendingNow,
            ongoingNow,
            completeToday,
            series));
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<OperatorNavAlertsResponse>> Alerts(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var pendingWallet = await db.RiderWalletTransactions.CountAsync(
            x => x.Rider.OperatorId == op.Id
                && x.Status == WalletTransactionStatus.Pending
                && (x.Kind == WalletTransactionKind.CashIn || x.Kind == WalletTransactionKind.CashOut),
            cancellationToken);
        var openSos = await db.SupportTickets.CountAsync(
            x => x.OperatorId == op.Id && x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var unreadBilling = await db.OperatorNotifications.CountAsync(
            x => x.OperatorId == op.Id && x.Kind == NotificationKind.Billing && x.ReadAtUtc == null,
            cancellationToken);
        var pendingAccountDeletes = await db.CustomerProfiles.CountAsync(
            x => x.DeleteStatus == DeleteAccountStatus.Pending
                && db.Trips.Any(t => t.OperatorId == op.Id && t.CustomerId == x.Id),
            cancellationToken);

        return Ok(new OperatorNavAlertsResponse(pendingWallet, openSos, unreadBilling, pendingAccountDeletes));
    }

    [HttpGet("fleet")]
    public async Task<ActionResult<OperatorFleetResponse>> Fleet(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        await broadcast.ExpireStaleOnlineRidersAsync(cancellationToken);
        var riders = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == op!.Id && x.IsActive && x.AppUser.IsActive)
            .OrderBy(x => x.AppUser.FullName)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var live = await db.Trips
            .Where(x =>
                x.OperatorId == op!.Id &&
                (x.Status == TripStatus.Pending || x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing) &&
                (x.ScheduledAtUtc == null || x.Status != TripStatus.Pending || x.ScheduledAtUtc <= now))
            .Select(x => new { x.RiderId, x.Status, x.Reference, x.RequestedAtUtc })
            .ToListAsync(cancellationToken);
        var duty = live
            .GroupBy(x => x.RiderId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.Status == TripStatus.Ongoing ? 0 : x.Status == TripStatus.Waiting ? 1 : 2)
                    .ThenByDescending(x => x.RequestedAtUtc)
                    .First());
        var onMap = riders
            .Where(x => x.LastLat is not null && x.LastLng is not null)
            .Select(x =>
            {
                duty.TryGetValue(x.Id, out var trip);
                return OperatorMaps.Fleet(x, trip?.Status, trip?.Reference);
            })
            .ToList();

        return Ok(new OperatorFleetResponse(
            riders.Count,
            onMap.Count,
            onMap.Count(x => x.VehicleType == VehicleType.Motorcycle),
            onMap.Count(x => x.VehicleType == VehicleType.Tricycle),
            onMap));
    }

    [HttpGet("company")]
    public async Task<ActionResult<OperatorDetailResponse>> Company(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var loaded = await db.Operators
            .Include(x => x.AddressBarangay)
            .ThenInclude(x => x!.Municipality)
            .ThenInclude(x => x.Province)
            .Include(x => x.Areas)
            .ThenInclude(x => x.Barangay)
            .ThenInclude(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Include(x => x.Riders)
            .ThenInclude(x => x.AppUser)
            .FirstAsync(x => x.Id == op!.Id, cancellationToken);

        var riders = loaded.Riders.OrderBy(x => x.AppUser.FullName).Select(OperatorMaps.Rider).ToList();
        return Ok(new OperatorDetailResponse(
            loaded.Id,
            loaded.CompanyName,
            loaded.ContactName,
            loaded.ContactPhone,
            loaded.FullAddress,
            loaded.AreaOfOperation,
            loaded.GovernmentIdType,
            loaded.GovernmentId,
            UploadUrls.FromPath(loaded.ProfilePhotoPath),
            UploadUrls.FromPath(loaded.GovernmentIdPhotoPath),
            loaded.IsActive,
            loaded.MotorcycleCommissionPercent,
            loaded.TricycleCommissionPercent,
            riders.Count,
            loaded.Riders.Count(x => x.VehicleType == VehicleType.Motorcycle),
            loaded.Riders.Count(x => x.VehicleType == VehicleType.Tricycle),
            loaded.CreatedAtUtc,
            YaPasakay.Infrastructure.Persistence.OperatorAddressSync.Map(loaded),
            YaPasakay.Infrastructure.Persistence.OperatorAreaSync.Map(loaded.Areas),
            riders));
    }

    [HttpGet("territories/provinces")]
    public async Task<ActionResult<IReadOnlyList<IdName>>> Provinces(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await TerritoryLookup.ProvincesAsync(db, cancellationToken));
    }

    [HttpGet("territories/municipalities")]
    public async Task<ActionResult<IReadOnlyList<IdName>>> Municipalities(
        [FromQuery] Guid provinceId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await TerritoryLookup.MunicipalitiesAsync(db, provinceId, cancellationToken));
    }

    [HttpGet("territories/barangays")]
    public async Task<ActionResult<IReadOnlyList<BarangayOption>>> Barangays(
        [FromQuery] Guid municipalityId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await TerritoryLookup.BarangaysAsync(db, municipalityId, cancellationToken));
    }
}
