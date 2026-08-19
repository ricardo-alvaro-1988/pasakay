using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Models;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;
using YaPasakay.Infrastructure.Services;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController(AppDbContext db, UploadStore uploads, IOtpStore otpStore) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewResponse>> Overview([FromQuery] string range = "weekly", CancellationToken cancellationToken = default)
    {
        var days = range.ToLowerInvariant() switch
        {
            "monthly" => 30,
            "yearly" => 365,
            _ => 7
        };

        var from = DateTime.UtcNow.Date.AddDays(1 - days);
        var operators = await db.Operators.CountAsync(cancellationToken);
        var riders = await db.RiderProfiles.CountAsync(cancellationToken);
        var ridersMc = await db.RiderProfiles.CountAsync(x => x.VehicleType == VehicleType.Motorcycle, cancellationToken);
        var ridersTrike = await db.RiderProfiles.CountAsync(x => x.VehicleType == VehicleType.Tricycle, cancellationToken);
        var customers = await db.CustomerProfiles.CountAsync(cancellationToken);
        var tripsToday = await db.Trips.CountAsync(
            x => x.Status == TripStatus.Completed && x.CompletedAtUtc >= DateTime.UtcNow.Date,
            cancellationToken);
        var adminCutToday = await db.Trips
            .Where(x => x.Status == TripStatus.Completed && x.CompletedAtUtc >= DateTime.UtcNow.Date)
            .SumAsync(x => (decimal?)(x.Fare * (x.VehicleType == VehicleType.Motorcycle
                ? x.Operator.MotorcycleCommissionPercent
                : x.Operator.TricycleCommissionPercent) / 100m), cancellationToken) ?? 0;
        var openSos = await db.SupportTickets.CountAsync(
            x => x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
            cancellationToken);
        var unreadSosAlerts = await db.AdminNotifications.CountAsync(
            x => x.Kind == NotificationKind.Sos && x.ReadAtUtc == null,
            cancellationToken);
        var pendingAccountDeletes = await db.CustomerProfiles.CountAsync(
            x => x.DeleteStatus == DeleteAccountStatus.Pending,
            cancellationToken);

        var operatorDays = await db.Operators
            .Where(x => x.CreatedAtUtc >= from)
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var customerDays = await db.CustomerProfiles
            .Where(x => x.CreatedAtUtc >= from)
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var tripDays = await db.Trips
            .Where(x => x.Status == TripStatus.Completed && x.CompletedAtUtc >= from)
            .GroupBy(x => x.CompletedAtUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var series = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = DateOnly.FromDateTime(from.AddDays(offset));
                var created = operatorDays.FirstOrDefault(x => DateOnly.FromDateTime(x.Day) == day)?.Count ?? 0;
                var registered = customerDays.FirstOrDefault(x => DateOnly.FromDateTime(x.Day) == day)?.Count ?? 0;
                var trips = tripDays.FirstOrDefault(x => DateOnly.FromDateTime(x.Day) == day)?.Count ?? 0;
                return new OverviewSeriesPoint(day, created, registered, trips);
            })
            .ToList();

        var recent = await db.Operators
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .Select(x => new
            {
                Operator = x,
                RiderCount = x.Riders.Count,
                Motorcycle = x.Riders.Count(r => r.VehicleType == VehicleType.Motorcycle),
                Tricycle = x.Riders.Count(r => r.VehicleType == VehicleType.Tricycle)
            })
            .ToListAsync(cancellationToken);

        return Ok(new OverviewResponse(
            operators,
            riders,
            ridersMc,
            ridersTrike,
            customers,
            tripsToday,
            Math.Round(adminCutToday, 2, MidpointRounding.AwayFromZero),
            openSos,
            unreadSosAlerts,
            pendingAccountDeletes,
            series,
            recent.Select(x => MapOperator(x.Operator, x.RiderCount, x.Motorcycle, x.Tricycle)).ToList()));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SearchHit>>> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var (_, pages) = await AdminAccess.ResolveAsync(db, User, cancellationToken);
        var allowOperators = pages.Contains("operators");
        var allowCustomers = pages.Contains("customers");

        var term = (q ?? string.Empty).Trim();
        var phone = PhoneNormalizer.Normalize(term);

        var operatorQuery = db.Operators.AsQueryable();
        var customerQuery = db.CustomerProfiles.Include(x => x.AppUser).AsQueryable();
        if (term.Length > 0)
        {
            operatorQuery = operatorQuery.Where(x =>
                x.CompanyName.Contains(term) ||
                x.ContactName.Contains(term) ||
                x.ContactPhone.Contains(phone.Length > 0 ? phone : term));
            customerQuery = customerQuery.Where(x =>
                x.FirstName.Contains(term) ||
                x.LastName.Contains(term) ||
                x.AppUser.FullName.Contains(term) ||
                x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term));
        }

        var operators = allowOperators
            ? await operatorQuery.OrderBy(x => x.CompanyName).Take(8).ToListAsync(cancellationToken)
            : [];

        var customers = allowCustomers
            ? await customerQuery.OrderBy(x => x.AppUser.FullName).Take(8).ToListAsync(cancellationToken)
            : [];

        var hits = operators
            .Select(x => new SearchHit("operator", x.Id, x.CompanyName, x.ContactPhone, UploadUrls.FromPath(x.ProfilePhotoPath)))
            .Concat(customers.Select(x => new SearchHit("customer", x.Id, CustomerDisplayName(x), x.AppUser.PhoneNumber, UploadUrls.FromPath(x.PhotoPath))))
            .Take(10)
            .ToList();

        return Ok(hits);
    }

    [HttpGet("operators")]
    public async Task<ActionResult<PagedResult<OperatorListItem>>> ListOperators(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Operators.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.CompanyName.Contains(term) ||
                x.ContactName.Contains(term) ||
                x.ContactPhone.Contains(phone.Length > 0 ? phone : term) ||
                x.AreaOfOperation.Contains(term) ||
                x.Areas.Any(a =>
                    a.Barangay.Name.Contains(term) ||
                    a.Barangay.Municipality.Name.Contains(term) ||
                    a.Barangay.Municipality.Province.Name.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                Operator = x,
                RiderCount = x.Riders.Count,
                Motorcycle = x.Riders.Count(r => r.VehicleType == VehicleType.Motorcycle),
                Tricycle = x.Riders.Count(r => r.VehicleType == VehicleType.Tricycle)
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<OperatorListItem>(
            items.Select(x => MapOperator(x.Operator, x.RiderCount, x.Motorcycle, x.Tricycle)).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("territories")]
    public async Task<ActionResult<PagedResult<TerritoryListItem>>> ListTerritories(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Municipalities.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                x.Province.Name.Contains(term) ||
                x.Barangays.Any(b => b.Name.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Province.Name)
            .ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ProvinceId,
                Province = x.Province.Name,
                Municipality = x.Name
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(x => x.Id).ToList();
        var barangays = ids.Count == 0
            ? []
            : await db.Barangays
                .Where(x => ids.Contains(x.MunicipalityId))
                .OrderBy(x => x.Name)
                .Select(x => new { x.MunicipalityId, x.Name })
                .ToListAsync(cancellationToken);
        var barangaysByMunicipality = barangays
            .GroupBy(x => x.MunicipalityId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

        var operatorCounts = ids.Count == 0
            ? []
            : await db.OperatorBarangays
                .Where(x => ids.Contains(x.Barangay.MunicipalityId))
                .Select(x => new { x.Barangay.MunicipalityId, x.OperatorId })
                .Distinct()
                .GroupBy(x => x.MunicipalityId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
        var operatorsByMunicipality = operatorCounts.ToDictionary(x => x.Key, x => x.Count);

        const int preview = 3;
        var items = rows.Select(row =>
        {
            var names = barangaysByMunicipality.GetValueOrDefault(row.Id) ?? [];
            return new TerritoryListItem(
                row.Id,
                row.ProvinceId,
                row.Province,
                row.Municipality,
                names.Take(preview).ToList(),
                names.Count,
                operatorsByMunicipality.GetValueOrDefault(row.Id));
        }).ToList();

        return Ok(new PagedResult<TerritoryListItem>(items, page, pageSize, total));
    }

    [HttpGet("territories/provinces")]
    public async Task<ActionResult<IReadOnlyList<IdName>>> ListProvinces(CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.ProvincesAsync(db, cancellationToken));

    [HttpGet("territories/municipalities")]
    public async Task<ActionResult<IReadOnlyList<IdName>>> ListMunicipalities(
        [FromQuery] Guid provinceId,
        CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.MunicipalitiesAsync(db, provinceId, cancellationToken));

    [HttpGet("territories/barangays")]
    public async Task<ActionResult<IReadOnlyList<BarangayOption>>> ListBarangays(
        [FromQuery] Guid municipalityId,
        CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.BarangaysAsync(db, municipalityId, cancellationToken));

    [HttpGet("fares")]
    public async Task<ActionResult<PagedResult<OperatorFareListItem>>> ListFares(
        [FromQuery] string? q,
        [FromQuery] VehicleType? vehicleType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Operators.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.CompanyName.Contains(term) || x.ContactName.Contains(term));
        }

        if (vehicleType.HasValue)
        {
            query = query.Where(x => x.FareMatrices.Any(f => f.VehicleType == vehicleType.Value));
        }

        var total = await query.CountAsync(cancellationToken);
        var operators = await query
            .OrderBy(x => x.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.CompanyName, x.IsActive, x.MotorcycleCommissionPercent, x.TricycleCommissionPercent })
            .ToListAsync(cancellationToken);

        var ids = operators.Select(x => x.Id).ToList();
        var fares = ids.Count == 0
            ? []
            : await db.FareMatrices
                .Include(x => x.Surcharges)
                .Where(x => ids.Contains(x.OperatorId))
                .ToListAsync(cancellationToken);

        var items = operators.Select(op =>
        {
            var rows = fares.Where(x => x.OperatorId == op.Id).ToList();
            return new OperatorFareListItem(
                op.Id,
                op.CompanyName,
                op.IsActive,
                op.MotorcycleCommissionPercent,
                op.TricycleCommissionPercent,
                MapFareRates(rows.FirstOrDefault(x => x.VehicleType == VehicleType.Motorcycle), includeSamples: false),
                MapFareRates(rows.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle), includeSamples: false));
        }).ToList();

        return Ok(new PagedResult<OperatorFareListItem>(items, page, pageSize, total));
    }

    [HttpGet("operators/{id:guid}/fares")]
    public async Task<ActionResult<OperatorFareDetailResponse>> GetOperatorFares(Guid id, CancellationToken cancellationToken)
    {
        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (op is null)
        {
            return NotFound();
        }

        var fares = await db.FareMatrices.Include(x => x.Surcharges).Where(x => x.OperatorId == id).ToListAsync(cancellationToken);
        return Ok(new OperatorFareDetailResponse(
            op.Id,
            op.CompanyName,
            op.IsActive,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            MapFareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Motorcycle), includeSamples: true),
            MapFareRates(fares.FirstOrDefault(x => x.VehicleType == VehicleType.Tricycle), includeSamples: true)));
    }

    [HttpPost("operators")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<OperatorListItem>> CreateOperator([FromForm] CreateOperatorForm form, CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(form.Phone);
        if (string.IsNullOrWhiteSpace(form.CompanyName) ||
            string.IsNullOrWhiteSpace(form.ContactName) ||
            phone.Length < 10 ||
            string.IsNullOrWhiteSpace(form.GovernmentIdType) ||
            string.IsNullOrWhiteSpace(form.GovernmentId))
        {
            return BadRequest(new { message = "Company, contact, phone, government ID type, and ID number are required." });
        }

        if (!GovernmentIdCatalog.IsValid(form.GovernmentIdType))
        {
            return BadRequest(new { message = "Choose a valid government ID type." });
        }

        var (mcOk, motorcycleCommission, mcError) = ParseCommission(form.MotorcycleCommissionPercent, "motorcycle");
        if (!mcOk)
        {
            return BadRequest(new { message = mcError });
        }

        var (trikeOk, tricycleCommission, trikeError) = ParseCommission(form.TricycleCommissionPercent, "tricycle");
        if (!trikeOk)
        {
            return BadRequest(new { message = trikeError });
        }

        if (!SecretHasher.IsStrongPassword(form.Password ?? string.Empty))
        {
            return BadRequest(new { message = "Set a password of at least 6 characters." });
        }

        if (await db.Users.AnyAsync(x => x.PhoneNumber == phone, cancellationToken) ||
            await db.Operators.AnyAsync(x => x.ContactPhone == phone, cancellationToken))
        {
            return Conflict(new { message = "That phone is already in use." });
        }

        var op = new Operator
        {
            CompanyName = form.CompanyName.Trim(),
            ContactName = form.ContactName.Trim(),
            ContactPhone = phone,
            GovernmentIdType = GovernmentIdCatalog.Normalize(form.GovernmentIdType),
            GovernmentId = form.GovernmentId.Trim(),
            IsActive = true,
            MotorcycleCommissionPercent = motorcycleCommission,
            TricycleCommissionPercent = tricycleCommission
        };

        var (addressOk, addressError) = await OperatorAddressSync.AssignAsync(db, op, form.AddressBarangayId, form.AddressDetails, cancellationToken);
        if (!addressOk)
        {
            return BadRequest(new { message = addressError });
        }

        var (areasOk, areaError) = await OperatorAreaSync.AssignAsync(db, op, form.BarangayIds, cancellationToken);
        if (!areasOk)
        {
            return BadRequest(new { message = areaError });
        }

        try
        {
            op.ProfilePhotoPath = await uploads.SaveAsync(form.ProfilePhoto, $"operators/{op.Id}", "profile", cancellationToken);
            op.GovernmentIdPhotoPath = await uploads.SaveAsync(form.GovernmentIdPhoto, $"operators/{op.Id}", "gov-id", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        db.Operators.Add(op);
        db.Users.Add(new AppUser
        {
            PhoneNumber = phone,
            FullName = form.ContactName.Trim(),
            PasswordHash = SecretHasher.Hash(form.Password!.Trim()),
            Role = UserRole.Operator,
            OperatorId = op.Id,
            IsActive = true
        });

        OperatorAudit.Record(db, User, op.Id, AuditAction.OperatorCreated, $"Created Operator {op.CompanyName}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapOperator(op, 0, 0, 0));
    }

    [HttpPut("operators/{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<OperatorListItem>> UpdateOperator(Guid id, [FromForm] CreateOperatorForm form, CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(form.Phone);
        if (string.IsNullOrWhiteSpace(form.CompanyName) ||
            string.IsNullOrWhiteSpace(form.ContactName) ||
            phone.Length < 10 ||
            string.IsNullOrWhiteSpace(form.GovernmentIdType) ||
            string.IsNullOrWhiteSpace(form.GovernmentId))
        {
            return BadRequest(new { message = "Company, contact, phone, government ID type, and ID number are required." });
        }

        if (!GovernmentIdCatalog.IsValid(form.GovernmentIdType))
        {
            return BadRequest(new { message = "Choose a valid government ID type." });
        }

        var (mcOk, motorcycleCommission, mcError) = ParseCommission(form.MotorcycleCommissionPercent, "motorcycle");
        if (!mcOk)
        {
            return BadRequest(new { message = mcError });
        }

        var (trikeOk, tricycleCommission, trikeError) = ParseCommission(form.TricycleCommissionPercent, "tricycle");
        if (!trikeOk)
        {
            return BadRequest(new { message = trikeError });
        }

        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (op is null)
        {
            return NotFound();
        }

        if (await db.Users.AnyAsync(x => x.PhoneNumber == phone && x.OperatorId != id, cancellationToken) ||
            await db.Operators.AnyAsync(x => x.ContactPhone == phone && x.Id != id, cancellationToken))
        {
            return Conflict(new { message = "That phone is already in use." });
        }

        var (addressOk, addressError) = await OperatorAddressSync.AssignAsync(db, op, form.AddressBarangayId, form.AddressDetails, cancellationToken);
        if (!addressOk)
        {
            return BadRequest(new { message = addressError });
        }

        var (areasOk, areaError) = await OperatorAreaSync.AssignAsync(db, op, form.BarangayIds, cancellationToken);
        if (!areasOk)
        {
            return BadRequest(new { message = areaError });
        }

        op.CompanyName = form.CompanyName.Trim();
        op.ContactName = form.ContactName.Trim();
        op.ContactPhone = phone;
        op.GovernmentIdType = GovernmentIdCatalog.Normalize(form.GovernmentIdType);
        op.GovernmentId = form.GovernmentId.Trim();
        op.MotorcycleCommissionPercent = motorcycleCommission;
        op.TricycleCommissionPercent = tricycleCommission;
        op.UpdatedAtUtc = DateTime.UtcNow;

        var fares = await db.FareMatrices.Where(x => x.OperatorId == op.Id).ToListAsync(cancellationToken);
        foreach (var fare in fares)
        {
            FareCommissionSplit.KeepOperatorShare(fare, FareCommissionSplit.SystemPercent(op, fare.VehicleType));
        }

        try
        {
            var profile = await uploads.SaveAsync(form.ProfilePhoto, $"operators/{op.Id}", "profile", cancellationToken);
            var gov = await uploads.SaveAsync(form.GovernmentIdPhoto, $"operators/{op.Id}", "gov-id", cancellationToken);
            if (profile is not null)
            {
                op.ProfilePhotoPath = profile;
            }
            if (gov is not null)
            {
                op.GovernmentIdPhotoPath = gov;
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var login = await db.Users.FirstOrDefaultAsync(x => x.OperatorId == id && x.Role == UserRole.Operator, cancellationToken);
        if (login is not null)
        {
            login.PhoneNumber = phone;
            login.FullName = form.ContactName.Trim();
            if (!string.IsNullOrWhiteSpace(form.Password))
            {
                if (!SecretHasher.IsStrongPassword(form.Password))
                {
                    return BadRequest(new { message = "Password must be at least 6 characters." });
                }

                login.PasswordHash = SecretHasher.Hash(form.Password.Trim());
            }
        }

        OperatorAudit.Record(db, User, op.Id, AuditAction.OperatorUpdated, $"Updated Operator {op.CompanyName}.");
        await db.SaveChangesAsync(cancellationToken);
        var riderCount = await db.RiderProfiles.CountAsync(x => x.OperatorId == id, cancellationToken);
        var mc = await db.RiderProfiles.CountAsync(x => x.OperatorId == id && x.VehicleType == VehicleType.Motorcycle, cancellationToken);
        var trike = await db.RiderProfiles.CountAsync(x => x.OperatorId == id && x.VehicleType == VehicleType.Tricycle, cancellationToken);
        return Ok(MapOperator(op, riderCount, mc, trike));
    }

    [HttpGet("operators/{id:guid}")]
    public async Task<ActionResult<OperatorDetailResponse>> GetOperator(Guid id, CancellationToken cancellationToken)
    {
        var op = await db.Operators
            .Include(x => x.AddressBarangay)
            .ThenInclude(x => x!.Municipality)
            .ThenInclude(x => x.Province)
            .Include(x => x.Areas)
            .ThenInclude(x => x.Barangay)
            .ThenInclude(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Include(x => x.Riders)
            .ThenInclude(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (op is null)
        {
            return NotFound();
        }

        var riders = op.Riders
            .OrderBy(x => x.AppUser.FullName)
            .Select(MapRider)
            .ToList();

        return Ok(new OperatorDetailResponse(
            op.Id,
            op.CompanyName,
            op.ContactName,
            op.ContactPhone,
            op.FullAddress,
            op.AreaOfOperation,
            op.GovernmentIdType,
            op.GovernmentId,
            UploadUrls.FromPath(op.ProfilePhotoPath),
            UploadUrls.FromPath(op.GovernmentIdPhotoPath),
            op.IsActive,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            riders.Count,
            op.Riders.Count(x => x.VehicleType == VehicleType.Motorcycle),
            op.Riders.Count(x => x.VehicleType == VehicleType.Tricycle),
            op.CreatedAtUtc,
            OperatorAddressSync.Map(op),
            OperatorAreaSync.Map(op.Areas),
            riders));
    }

    [HttpGet("operators/{id:guid}/bookings")]
    public async Task<ActionResult<PagedResult<OperatorBookingListItem>>> ListOperatorBookings(
        Guid id,
        [FromQuery] string? q,
        [FromQuery(Name = "status")] TripStatus? tripStatus,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Operators.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return NotFound();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Trips
            .AsNoTracking()
            .Where(x => x.OperatorId == id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            var phoneTerm = phone.Length > 0 ? phone : term;
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.CustomerPhone.Contains(phoneTerm) ||
                x.Pickup.Contains(term) ||
                x.Dropoff.Contains(term) ||
                x.Rider.AppUser.FullName.Contains(term) ||
                x.Rider.PlateNumber.Contains(term));
        }

        if (tripStatus is TripStatus filterStatus)
        {
            query = query.Where(x => x.Status == filterStatus);
        }

        if (from is not null || to is not null)
        {
            var today = DateOnly.FromDateTime(PhilippineTime.ToPh(DateTime.UtcNow));
            var startDate = from ?? to ?? today;
            var endDate = to ?? from ?? today;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var start = PhilippineTime.ToUtc(startDate.Year, startDate.Month, startDate.Day);
            var next = endDate.AddDays(1);
            var endExclusive = PhilippineTime.ToUtc(next.Year, next.Month, next.Day);
            query = query.Where(x =>
                (x.ScheduledAtUtc ?? x.RequestedAtUtc) >= start
                && (x.ScheduledAtUtc ?? x.RequestedAtUtc) < endExclusive);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Status == TripStatus.Completed || x.Status == TripStatus.Cancelled ? 2 : 0)
            .ThenByDescending(x => x.ScheduledAtUtc ?? x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OperatorBookingListItem(
                x.Id,
                x.Reference,
                x.RequestedAtUtc,
                x.ScheduledAtUtc,
                x.CustomerName,
                x.CustomerPhone,
                x.Rider.AppUser.FullName,
                x.Rider.PlateNumber,
                x.VehicleType,
                x.Pickup,
                x.Dropoff,
                x.Status,
                x.Fare,
                x.PaymentMethod,
                x.PaymentMethodOther))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<OperatorBookingListItem>(
            rows.Select(x => x with
            {
                RequestedAtUtc = DateTime.SpecifyKind(x.RequestedAtUtc, DateTimeKind.Utc),
                ScheduledAtUtc = x.ScheduledAtUtc is DateTime scheduled
                    ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc)
                    : null
            }).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("operators/{id:guid}/bookings/{bookingId:guid}")]
    public async Task<ActionResult<RideDetailResponse>> GetOperatorBooking(
        Guid id,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var trip = await RideDetailQuery()
            .FirstOrDefaultAsync(x => x.OperatorId == id && x.Id == bookingId, cancellationToken);
        return trip is null ? NotFound() : Ok(MapRideDetail(trip));
    }

    [HttpGet("operators/{id:guid}/riders")]
    public async Task<ActionResult<PagedResult<RiderListItem>>> ListRiders(
        Guid id,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Operators.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return NotFound();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.RiderProfiles.Where(x => x.OperatorId == id).Include(x => x.AppUser).Include(x => x.PaymentMethods).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.AppUser.FullName.Contains(term) ||
                x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term) ||
                x.PlateNumber.Contains(term) ||
                (x.LicenseNumber != null && x.LicenseNumber.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.AppUser.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<RiderListItem>(rows.Select(MapRider).ToList(), page, pageSize, total));
    }

    [HttpGet("operators/{id:guid}/riders/{riderId:guid}")]
    public async Task<ActionResult<RiderDetailResponse>> GetRider(Guid id, Guid riderId, CancellationToken cancellationToken)
    {
        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AddressBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.OperatorId == id && x.Id == riderId, cancellationToken);

        return rider is null ? NotFound() : Ok(MapRiderDetail(rider));
    }

    [HttpPost("operators/{id:guid}/riders/{riderId:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetRiderPassword(
        Guid id,
        Guid riderId,
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.OperatorId == id && x.Id == riderId, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        var (result, error) = await LoginReset.SetPasswordAsync(db, rider.AppUser, request.Password, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        OperatorAudit.Record(
            db,
            User,
            id,
            AuditAction.OperatorUpdated,
            $"Reset sign-in for rider {rider.AppUser.FullName}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("operators/{id:guid}/riders/{riderId:guid}/rides")]
    public async Task<ActionResult<RiderRidesResponse>> ListRiderRides(
        Guid id,
        Guid riderId,
        [FromQuery] string range = "weekly",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? q = null,
        [FromQuery] TripStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await db.RiderProfiles.AnyAsync(x => x.OperatorId == id && x.Id == riderId, cancellationToken))
        {
            return NotFound();
        }

        return await BuildRidesResponse(db.Trips.Where(x => x.RiderId == riderId), range, from, to, q, status, page, pageSize, cancellationToken);
    }

    [HttpGet("operators/{id:guid}/riders/{riderId:guid}/rides/{rideId:guid}")]
    public async Task<ActionResult<RideDetailResponse>> GetRide(
        Guid id,
        Guid riderId,
        Guid rideId,
        CancellationToken cancellationToken)
    {
        var trip = await RideDetailQuery()
            .FirstOrDefaultAsync(
                x => x.OperatorId == id && x.RiderId == riderId && x.Id == rideId,
                cancellationToken);

        return trip is null ? NotFound() : Ok(MapRideDetail(trip));
    }

    private static (DateTime Start, DateTime EndExclusive, int Days) ResolveRideWindow(
        string range,
        DateOnly? from,
        DateOnly? to)
    {
        if (from.HasValue || to.HasValue)
        {
            var startDate = from ?? to!.Value;
            var endDate = to ?? from!.Value;
            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var days = Math.Clamp(endDate.DayNumber - startDate.DayNumber + 1, 1, 366);
            endDate = startDate.AddDays(days - 1);
            var start = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return (start, start.AddDays(days), days);
        }

        var presetDays = range.ToLowerInvariant() switch
        {
            "monthly" => 30,
            "yearly" => 365,
            _ => 7
        };
        var presetStart = DateTime.UtcNow.Date.AddDays(1 - presetDays);
        return (presetStart, DateTime.UtcNow.Date.AddDays(1), presetDays);
    }

    [HttpPost("operators/{id:guid}/active")]
    public async Task<IActionResult> SetOperatorActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (op is null)
        {
            return NotFound();
        }

        op.IsActive = request.IsActive;
        op.UpdatedAtUtc = DateTime.UtcNow;
        await db.Users
            .Where(x => x.OperatorId == id && x.Role == UserRole.Operator)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, request.IsActive), cancellationToken);
        OperatorAudit.Record(
            db,
            User,
            op.Id,
            request.IsActive ? AuditAction.OperatorActivated : AuditAction.OperatorDeactivated,
            request.IsActive
                ? $"Activated Operator {op.CompanyName}."
                : $"Deactivated Operator {op.CompanyName}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { op.Id, op.IsActive });
    }

    [HttpPost("operators/{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetOperatorPassword(
        Guid id,
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var op = await db.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (op is null)
        {
            return NotFound();
        }

        var user = await db.Users.FirstOrDefaultAsync(
            x => x.OperatorId == id && x.Role == UserRole.Operator,
            cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "This Operator has no login account." });
        }

        var (result, error) = await LoginReset.SetPasswordAsync(db, user, request.Password, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        OperatorAudit.Record(db, User, op.Id, AuditAction.OperatorUpdated, $"Reset sign-in for Operator {op.CompanyName}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerListItem>>> ListCustomers([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = db.CustomerProfiles.Include(x => x.AppUser).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.FirstName.Contains(term) ||
                x.LastName.Contains(term) ||
                x.AppUser.FullName.Contains(term) ||
                x.AppUser.PhoneNumber.Contains(phone.Length > 0 ? phone : term));
        }

        var rows = await query
            .OrderByDescending(x => x.DeleteStatus == DeleteAccountStatus.Pending)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(MapCustomer).ToList());
    }

    [HttpGet("customers/{id:guid}")]
    public async Task<ActionResult<CustomerDetailResponse>> GetCustomer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return customer is null ? NotFound() : Ok(MapCustomerDetail(customer));
    }

    [HttpPost("customers/{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetCustomerPassword(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        var result = await LoginReset.ResetAsync(db, otpStore, customer.AppUser, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("customers/{id:guid}/rides")]
    public async Task<ActionResult<RiderRidesResponse>> ListCustomerRides(
        Guid id,
        [FromQuery] string range = "weekly",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? q = null,
        [FromQuery] TripStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!await db.CustomerProfiles.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return NotFound();
        }

        return await BuildRidesResponse(db.Trips.Where(x => x.CustomerId == id), range, from, to, q, status, page, pageSize, cancellationToken);
    }

    [HttpGet("customers/{id:guid}/rides/{rideId:guid}")]
    public async Task<ActionResult<RideDetailResponse>> GetCustomerRide(Guid id, Guid rideId, CancellationToken cancellationToken)
    {
        var trip = await RideDetailQuery()
            .FirstOrDefaultAsync(x => x.CustomerId == id && x.Id == rideId, cancellationToken);
        return trip is null ? NotFound() : Ok(MapRideDetail(trip));
    }

    [HttpPost("customers/{id:guid}/delete-request")]
    public async Task<ActionResult<CustomerDetailResponse>> RecordDeleteRequest(
        Guid id,
        [FromBody] RecordDeleteRequest? request,
        CancellationToken cancellationToken)
    {
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (customer.DeleteStatus == DeleteAccountStatus.Approved)
        {
            return BadRequest(new { message = "This account deletion was already approved." });
        }

        customer.DeleteStatus = DeleteAccountStatus.Pending;
        customer.DeleteRequestedAtUtc = DateTime.UtcNow;
        customer.DeleteRequestReason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Customer requested account deletion."
            : request.Reason.Trim();
        customer.DeleteResolvedAtUtc = null;
        customer.DeleteResolutionNote = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await CustomerDeleteAlerts.NotifyAsync(db, customer, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapCustomerDetail(customer));
    }

    [HttpPost("customers/{id:guid}/delete-request/resolve")]
    public async Task<ActionResult<CustomerDetailResponse>> ResolveDeleteRequest(
        Guid id,
        [FromBody] ResolveDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (customer.DeleteStatus != DeleteAccountStatus.Pending)
        {
            return BadRequest(new { message = "There is no pending delete request for this customer." });
        }

        customer.DeleteStatus = request.Approve ? DeleteAccountStatus.Approved : DeleteAccountStatus.Rejected;
        customer.DeleteResolvedAtUtc = DateTime.UtcNow;
        customer.DeleteResolutionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        customer.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Approve)
        {
            customer.AppUser.IsActive = false;
            customer.AppUser.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapCustomerDetail(customer));
    }

    [HttpGet("government-id-types")]
    public ActionResult<IReadOnlyList<string>> ListGovernmentIdTypes() => Ok(GovernmentIdCatalog.All);

    private static OperatorListItem MapOperator(Operator op, int riderCount, int motorcycle, int tricycle) =>
        new(
            op.Id,
            op.CompanyName,
            op.ContactName,
            op.ContactPhone,
            op.FullAddress,
            op.AreaOfOperation,
            op.GovernmentIdType,
            op.GovernmentId,
            UploadUrls.FromPath(op.ProfilePhotoPath),
            UploadUrls.FromPath(op.GovernmentIdPhotoPath),
            op.IsActive,
            op.MotorcycleCommissionPercent,
            op.TricycleCommissionPercent,
            riderCount,
            motorcycle,
            tricycle,
            op.CreatedAtUtc);

    private static RiderListItem MapRider(RiderProfile rider) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.VehicleType,
            rider.PlateNumber,
            rider.VehicleModel,
            rider.IsActive,
            rider.LicenseType,
            rider.LicenseNumber,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            UploadUrls.FromPath(rider.LicensePhotoPath),
            RiderPaymentSync.Map(rider.PaymentMethods));

    private static RiderDetailResponse MapRiderDetail(RiderProfile rider) =>
        new(
            rider.Id,
            rider.AppUser.FullName,
            rider.AppUser.PhoneNumber,
            rider.VehicleType,
            rider.PlateNumber,
            rider.VehicleModel,
            rider.IsActive,
            rider.LicenseType,
            rider.LicenseNumber,
            UploadUrls.FromPath(rider.ProfilePhotoPath),
            UploadUrls.FromPath(rider.LicensePhotoPath),
            rider.FullAddress,
            OperatorAddressSync.Map(rider),
            RiderPaymentSync.Map(rider.PaymentMethods));

    private static RideDetailResponse MapRideDetail(Trip trip)
    {
        DateTime? ended = trip.Status switch
        {
            TripStatus.Completed => trip.CompletedAtUtc,
            TripStatus.Cancelled => trip.CancelledAtUtc,
            _ => DateTime.UtcNow
        };
        int? duration = ended is { } at
            ? Math.Max(1, (int)Math.Round((at - trip.RequestedAtUtc).TotalMinutes))
            : null;

        return new RideDetailResponse(
            trip.Id,
            trip.Reference,
            trip.Status,
            trip.CustomerName,
            trip.CustomerPhone,
            MapRideStop(trip.PickupDetails, trip.Pickup, trip.PickupBarangay),
            MapRideStop(trip.DropoffDetails, trip.Dropoff, trip.DropoffBarangay),
            trip.Pickup,
            trip.Dropoff,
            trip.Notes,
            trip.Fare,
            trip.DistanceKm,
            duration,
            trip.VehicleType,
            trip.RequestedAtUtc,
            trip.ScheduledAtUtc is DateTime scheduled ? DateTime.SpecifyKind(scheduled, DateTimeKind.Utc) : null,
            trip.CompletedAtUtc,
            trip.CancelledAtUtc,
            trip.CancelReason,
            trip.Rating,
            trip.RatingComment,
            trip.RatedAtUtc,
            trip.PaymentMethod,
            trip.PaymentMethodOther,
            trip.OperatorId,
            trip.Operator.CompanyName,
            trip.Operator.ContactPhone,
            trip.RiderId,
            trip.Rider.AppUser.FullName,
            trip.Rider.AppUser.PhoneNumber,
            trip.Rider.PlateNumber,
            trip.Rider.VehicleModel,
            UploadUrls.FromPath(trip.Rider.ProfilePhotoPath),
            trip.ChatMessages
                .OrderBy(x => x.SentAtUtc)
                .Select(TripChatService.Map)
                .ToList());
    }

    private static RideStopItem MapRideStop(string details, string fullAddress, Barangay? barangay) =>
        new(
            string.IsNullOrWhiteSpace(details) ? fullAddress : details,
            barangay?.Name ?? string.Empty,
            barangay?.Municipality.Name ?? string.Empty,
            barangay?.Municipality.Province.Name ?? string.Empty,
            fullAddress);

    private IQueryable<Trip> RideDetailQuery() =>
        db.Trips
            .Include(x => x.Operator)
            .Include(x => x.Rider)
            .ThenInclude(x => x.AppUser)
            .Include(x => x.PickupBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .Include(x => x.DropoffBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .Include(x => x.ChatMessages);

    private async Task<ActionResult<RiderRidesResponse>> BuildRidesResponse(
        IQueryable<Trip> source,
        string range,
        DateOnly? from,
        DateOnly? to,
        string? q,
        TripStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var term = q?.Trim() ?? "";
        var query = source;
        int days;
        DateTime start;
        if (term.Length > 0)
        {
            query = query.Where(x =>
                x.Reference.Contains(term) ||
                x.CustomerName.Contains(term) ||
                x.Pickup.Contains(term) ||
                x.PickupDetails.Contains(term) ||
                x.Dropoff.Contains(term) ||
                x.DropoffDetails.Contains(term));
            if (await query.AnyAsync(cancellationToken))
            {
                start = (await query.MinAsync(x => x.RequestedAtUtc, cancellationToken)).Date;
                var end = (await query.MaxAsync(x => x.RequestedAtUtc, cancellationToken)).Date;
                days = Math.Clamp((end - start).Days + 1, 1, 366);
            }
            else
            {
                start = DateTime.UtcNow.Date;
                days = 1;
            }
        }
        else
        {
            (start, var endExclusive, days) = ResolveRideWindow(range, from, to);
            query = query.Where(x => x.RequestedAtUtc >= start && x.RequestedAtUtc < endExclusive);
        }

        if (status is TripStatus tripStatus)
        {
            query = query.Where(x => x.Status == tripStatus);
        }

        var summary = new RiderRideSummary(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Completed, cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Cancelled, cancellationToken),
            await query.CountAsync(x => x.Status == TripStatus.Ongoing, cancellationToken),
            await query.Where(x => x.Status == TripStatus.Completed).SumAsync(x => (decimal?)x.Fare, cancellationToken) ?? 0);

        var tripDays = await query
            .Where(x => x.Status == TripStatus.Completed && x.CompletedAtUtc != null)
            .GroupBy(x => x.CompletedAtUtc!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var series = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = DateOnly.FromDateTime(start.AddDays(offset));
                var count = tripDays.FirstOrDefault(x => DateOnly.FromDateTime(x.Day) == day)?.Count ?? 0;
                return new RideSeriesPoint(day, count);
            })
            .ToList();

        var total = await query.CountAsync(cancellationToken);
        var rides = await query
            .OrderByDescending(x => x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RideListItem(
                x.Id,
                x.Reference,
                x.RequestedAtUtc,
                x.Pickup,
                x.Dropoff,
                x.CustomerName,
                x.VehicleType,
                x.Status,
                x.Fare,
                x.DistanceKm,
                x.PaymentMethod,
                x.PaymentMethodOther))
            .ToListAsync(cancellationToken);

        return Ok(new RiderRidesResponse(summary, series, new PagedResult<RideListItem>(rides, page, pageSize, total)));
    }

    private static CustomerListItem MapCustomer(CustomerProfile customer) =>
        new(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            CustomerDisplayName(customer),
            customer.AppUser.PhoneNumber,
            customer.CreatedAtUtc,
            customer.AppUser.IsActive,
            UploadUrls.FromPath(customer.PhotoPath),
            customer.DeleteStatus);

    private static CustomerDetailResponse MapCustomerDetail(CustomerProfile customer) =>
        new(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            CustomerDisplayName(customer),
            customer.AppUser.PhoneNumber,
            customer.CreatedAtUtc,
            customer.AppUser.IsActive,
            UploadUrls.FromPath(customer.PhotoPath),
            new CustomerDeleteRequestItem(
                customer.DeleteStatus,
                customer.DeleteRequestedAtUtc,
                customer.DeleteRequestReason,
                customer.DeleteResolvedAtUtc,
                customer.DeleteResolutionNote));

    private static string CustomerDisplayName(CustomerProfile customer)
    {
        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        return name.Length > 0 ? name : customer.AppUser.FullName;
    }

    private static FareRatesItem? MapFareRates(FareMatrix? fare, bool includeSamples)
    {
        if (fare is null)
        {
            return null;
        }

        return new FareRatesItem(
            fare.VehicleType,
            fare.BaseFare,
            fare.PerKm,
            fare.MinimumFare,
            fare.IncludedKm,
            fare.OperatorCommissionPercent,
            fare.DriverCommissionPercent,
            fare.IsActive,
            fare.Surcharges
                .OrderBy(x => x.Kind)
                .ThenBy(x => x.Name)
                .Select(x => new FareSurchargeItem(
                    x.Id,
                    x.Kind,
                    x.Name,
                    x.Amount,
                    x.WindowStart?.ToString("HH\\:mm"),
                    x.WindowEnd?.ToString("HH\\:mm"),
                    x.RangeStartUtc is DateTime start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null,
                    x.RangeEndUtc is DateTime end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
                    x.IsActive))
                .ToList(),
            includeSamples
                ? FareQuote.Samples(fare.BaseFare, fare.PerKm, fare.MinimumFare, fare.IncludedKm)
                : []);
    }

    private static (bool Ok, decimal Value, string Error) ParseCommission(decimal? raw, string vehicle)
    {
        if (raw is null)
        {
            return (false, 0, $"Set a {vehicle} platform commission from 0 to 100.");
        }

        var value = Math.Round(raw.Value, 2, MidpointRounding.AwayFromZero);
        if (value < 0 || value > 100)
        {
            return (false, 0, $"{char.ToUpperInvariant(vehicle[0])}{vehicle[1..]} platform commission must be between 0 and 100.");
        }

        return (true, value, string.Empty);
    }

    [HttpPost("profile/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] RiderPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = AdminAccess.UserId(User);
        var user = userId is null
            ? null
            : await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.Admin, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Admin account not found or inactive." });
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash)
            || !SecretHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (!SecretHasher.IsStrongPassword(request.NewPassword))
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        user.PasswordHash = SecretHasher.Hash(request.NewPassword.Trim());
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password updated." });
    }
}
