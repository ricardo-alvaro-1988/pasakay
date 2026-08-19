using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Models;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/riders")]
public class OperatorRidersController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<RiderListItem>>> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.RiderProfiles.Where(x => x.OperatorId == op!.Id).Include(x => x.AppUser).Include(x => x.PaymentMethods).AsQueryable();
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
        return Ok(new PagedResult<RiderListItem>(rows.Select(OperatorMaps.Rider).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RiderDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AddressBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        return rider is null ? NotFound() : Ok(OperatorMaps.RiderDetail(rider));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<RiderDetailResponse>> Create(
        [FromForm] CreateRiderForm form,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var parsed = await ParseAsync(form, null, cancellationToken);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        if (!SecretHasher.IsStrongPassword(form.Password ?? string.Empty))
        {
            return BadRequest(new { message = "Set a password of at least 6 characters." });
        }

        var user = new AppUser
        {
            FullName = parsed.Name,
            PhoneNumber = parsed.Phone,
            PasswordHash = SecretHasher.Hash(form.Password!.Trim()),
            Role = UserRole.Rider,
            OperatorId = op!.Id,
            IsActive = true
        };
        var rider = new RiderProfile
        {
            AppUser = user,
            OperatorId = op.Id,
            VehicleType = form.VehicleType,
            PlateNumber = parsed.Plate,
            VehicleModel = parsed.Model,
            LicenseType = parsed.LicenseType,
            LicenseNumber = parsed.LicenseNumber,
            IsActive = true
        };
        var address = await OperatorAddressSync.AssignAsync(db, rider, form.AddressBarangayId, form.AddressDetails, cancellationToken);
        if (!address.Ok)
        {
            return BadRequest(new { message = address.Error });
        }

        db.Users.Add(user);
        db.RiderProfiles.Add(rider);
        await db.SaveChangesAsync(cancellationToken);

        var paymentSync = await RiderPaymentSync.SyncAsync(db, rider, form.AcceptedPaymentMethods, cancellationToken);
        if (!paymentSync.Ok)
        {
            return BadRequest(new { message = paymentSync.Error });
        }

        try
        {
            rider.ProfilePhotoPath = await uploads.SaveAsync(form.ProfilePhoto, "riders", $"{rider.Id}-profile", cancellationToken);
            rider.LicensePhotoPath = await uploads.SaveAsync(form.LicensePhoto, "riders", $"{rider.Id}-license", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(await LoadDetailAsync(op.Id, rider.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<RiderDetailResponse>> Update(
        Guid id,
        [FromForm] CreateRiderForm form,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        var parsed = await ParseAsync(form, rider.AppUserId, cancellationToken);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        var address = await OperatorAddressSync.AssignAsync(db, rider, form.AddressBarangayId, form.AddressDetails, cancellationToken);
        if (!address.Ok)
        {
            return BadRequest(new { message = address.Error });
        }

        rider.AppUser.FullName = parsed.Name;
        rider.AppUser.PhoneNumber = parsed.Phone;
        if (!string.IsNullOrWhiteSpace(form.Password))
        {
            if (!SecretHasher.IsStrongPassword(form.Password))
            {
                return BadRequest(new { message = "Password must be at least 6 characters." });
            }

            rider.AppUser.PasswordHash = SecretHasher.Hash(form.Password.Trim());
        }

        rider.AppUser.UpdatedAtUtc = DateTime.UtcNow;
        rider.VehicleType = form.VehicleType;
        rider.PlateNumber = parsed.Plate;
        rider.VehicleModel = parsed.Model;
        rider.LicenseType = parsed.LicenseType;
        rider.LicenseNumber = parsed.LicenseNumber;
        rider.UpdatedAtUtc = DateTime.UtcNow;

        var paymentSync = await RiderPaymentSync.SyncAsync(db, rider, form.AcceptedPaymentMethods, cancellationToken);
        if (!paymentSync.Ok)
        {
            return BadRequest(new { message = paymentSync.Error });
        }

        try
        {
            rider.ProfilePhotoPath = await uploads.SaveAsync(form.ProfilePhoto, "riders", $"{rider.Id}-profile", cancellationToken)
                ?? rider.ProfilePhotoPath;
            rider.LicensePhotoPath = await uploads.SaveAsync(form.LicensePhoto, "riders", $"{rider.Id}-license", cancellationToken)
                ?? rider.LicensePhotoPath;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(op.Id, rider.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/active")]
    public async Task<ActionResult<RiderDetailResponse>> SetActive(
        Guid id,
        [FromBody] SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        rider.IsActive = request.IsActive;
        rider.AppUser.IsActive = request.IsActive;
        rider.UpdatedAtUtc = DateTime.UtcNow;
        rider.AppUser.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(op.Id, rider.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResult>> ResetPassword(
        Guid id,
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (rider is null)
        {
            return NotFound();
        }

        var (result, error) = await LoginReset.SetPasswordAsync(db, rider.AppUser, request.Password, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/rides")]
    public async Task<ActionResult<RiderRidesResponse>> Rides(
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
        var (op, statusCode, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(statusCode, new { message });
        }

        if (!await db.RiderProfiles.AnyAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await OperatorMaps.BuildRidesAsync(
            db.Trips.Where(x => x.OperatorId == op.Id && x.RiderId == id),
            range,
            from,
            to,
            q,
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:guid}/rides/{rideId:guid}")]
    public async Task<ActionResult<RideDetailResponse>> Ride(Guid id, Guid rideId, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var trip = await OperatorMaps.RideDetailQuery(db)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.RiderId == id && x.Id == rideId, cancellationToken);
        return trip is null ? NotFound() : Ok(OperatorMaps.RideDetail(trip));
    }

    private async Task<RiderDetailResponse> LoadDetailAsync(Guid operatorId, Guid riderId, CancellationToken cancellationToken)
    {
        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Include(x => x.PaymentMethods)
            .Include(x => x.AddressBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .FirstAsync(x => x.OperatorId == operatorId && x.Id == riderId, cancellationToken);
        return OperatorMaps.RiderDetail(rider);
    }

    private async Task<(string Name, string Phone, string Plate, string? Model, string LicenseType, string LicenseNumber, string? Error)> ParseAsync(
        CreateRiderForm form,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var name = (form.FullName ?? string.Empty).Trim();
        var phone = PhoneNormalizer.Normalize(form.Phone);
        var plate = (form.PlateNumber ?? string.Empty).Trim().ToUpperInvariant();
        var licenseType = (form.LicenseType ?? string.Empty).Trim();
        var licenseNumber = (form.LicenseNumber ?? string.Empty).Trim();
        if (name.Length == 0 || phone.Length < 10 || plate.Length == 0 || licenseType.Length == 0 || licenseNumber.Length == 0)
        {
            return ("", "", "", null, "", "", "Name, phone, plate, license type, and license number are required.");
        }

        if (form.VehicleType is not VehicleType.Motorcycle and not VehicleType.Tricycle)
        {
            return ("", "", "", null, "", "", "Choose Motorcycle or Tricycle.");
        }

        var taken = await db.Users.AnyAsync(
            x => x.PhoneNumber == phone && (userId == null || x.Id != userId),
            cancellationToken);
        if (taken)
        {
            return ("", "", "", null, "", "", "That phone is already in use.");
        }

        var model = string.IsNullOrWhiteSpace(form.VehicleModel) ? null : form.VehicleModel.Trim();
        return (name, phone, plate, model, licenseType, licenseNumber, null);
    }
}
