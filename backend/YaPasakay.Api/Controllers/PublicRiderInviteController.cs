using System.Security.Cryptography;
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
[AllowAnonymous]
[Route("api/public/rider-invite")]
public class PublicRiderInviteController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    public const string JoinPathPrefix = "/ops/#/rider-join/";
    public const string StatusPath = "/ops/#/rider-status";

    [HttpGet("{token}")]
    public async Task<ActionResult<RiderInvitePublicInfo>> Get(string token, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteAsync(token, cancellationToken);
        if (invite is null)
        {
            return NotFound(new { message = "This invite link is invalid or no longer active." });
        }

        return Ok(new RiderInvitePublicInfo(invite.Token, invite.Operator.CompanyName, StatusPath));
    }

    [HttpPost("{token}/apply")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> Apply(string token, [FromForm] CreateRiderForm form, CancellationToken cancellationToken)
    {
        var invite = await FindActiveInviteAsync(token, cancellationToken);
        if (invite is null)
        {
            return NotFound(new { message = "This invite link is invalid or no longer active." });
        }

        if (!SecretHasher.IsStrongPassword(form.Password ?? string.Empty))
        {
            return BadRequest(new { message = "Set a password of at least 6 characters." });
        }

        var parsed = await ParseAsync(form, cancellationToken);
        if (parsed.Error is not null)
        {
            return BadRequest(new { message = parsed.Error });
        }

        if (form.AcceptedPaymentMethods.Count == 0)
        {
            return BadRequest(new { message = "Select at least one payment method you accept." });
        }

        var pendingSamePhone = await db.RiderApplications.AnyAsync(
            x => x.PhoneNumber == parsed.Phone && x.Status == RiderApplicationStatus.Pending,
            cancellationToken);
        if (pendingSamePhone)
        {
            return BadRequest(new { message = "You already have a pending registration. Check your status with your mobile number." });
        }

        var barangay = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.Id == form.AddressBarangayId, cancellationToken);
        if (barangay is null)
        {
            return BadRequest(new { message = "Choose a full address." });
        }

        var details = (form.AddressDetails ?? string.Empty).Trim();
        if (details.Length == 0)
        {
            return BadRequest(new { message = "Add specific address details such as street, building, or unit." });
        }

        var application = new RiderApplication
        {
            OperatorId = invite.OperatorId,
            InviteLinkId = invite.Id,
            Status = RiderApplicationStatus.Pending,
            FullName = parsed.Name,
            PhoneNumber = parsed.Phone,
            PasswordHash = SecretHasher.Hash(form.Password!.Trim()),
            VehicleType = form.VehicleType,
            PlateNumber = parsed.Plate,
            VehicleFranchiseNumber = parsed.Franchise,
            VehicleModel = parsed.Model,
            LicenseType = parsed.LicenseType,
            LicenseNumber = parsed.LicenseNumber,
            AddressBarangayId = barangay.Id,
            AddressDetails = details,
            FullAddress = OperatorAddressSync.Format(details, barangay),
            AcceptedPaymentMethods = string.Join(",", form.AcceptedPaymentMethods.Distinct().Select(x => ((int)x).ToString()))
        };

        db.RiderApplications.Add(application);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            application.ProfilePhotoPath = await uploads.SaveAsync(
                form.ProfilePhoto, "rider-applications", $"{application.Id}-profile", cancellationToken);
            application.LicensePhotoPath = await uploads.SaveAsync(
                form.LicensePhoto, "rider-applications", $"{application.Id}-license", cancellationToken);
            application.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new
        {
            message = "Registration submitted. Keep your mobile number and password for the rider app after approval.",
            status = nameof(RiderApplicationStatus.Pending),
            statusPath = StatusPath
        });
    }

    [HttpPost("~/api/public/rider-application/status")]
    public async Task<ActionResult<RiderApplicationStatusResponse>> Status(
        [FromBody] RiderApplicationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(request.Phone ?? string.Empty);
        if (phone.Length < 10)
        {
            return BadRequest(new { message = "Enter a valid mobile number." });
        }

        var riderUser = await db.Users
            .AsNoTracking()
            .Include(x => x.Operator)
            .Where(x => x.Role == UserRole.Rider && x.PhoneNumber == phone)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (riderUser is not null)
        {
            return Ok(new RiderApplicationStatusResponse(
                "Approved",
                "Approved",
                riderUser.Operator?.CompanyName,
                "Your rider account is active. Sign in to the rider app with this mobile number and your password."));
        }

        var application = await db.RiderApplications
            .AsNoTracking()
            .Include(x => x.Operator)
            .Where(x => x.PhoneNumber == phone)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (application is null)
        {
            return Ok(new RiderApplicationStatusResponse(
                "NoRegistration",
                "No registration",
                null,
                "No rider registration was found for this mobile number."));
        }

        return application.Status switch
        {
            RiderApplicationStatus.Pending => Ok(new RiderApplicationStatusResponse(
                "Pending",
                "Pending",
                application.Operator.CompanyName,
                "Your registration is waiting for operator review.")),
            RiderApplicationStatus.Rejected => Ok(new RiderApplicationStatusResponse(
                "Rejected",
                "Rejected",
                application.Operator.CompanyName,
                string.IsNullOrWhiteSpace(application.ReviewNote)
                    ? "Your registration was not approved."
                    : application.ReviewNote)),
            RiderApplicationStatus.Approved => Ok(new RiderApplicationStatusResponse(
                "Approved",
                "Approved",
                application.Operator.CompanyName,
                "Your rider account is active. Sign in to the rider app with this mobile number and your password.")),
            _ => Ok(new RiderApplicationStatusResponse(
                "NoRegistration",
                "No registration",
                null,
                "No rider registration was found for this mobile number."))
        };
    }

    private async Task<RiderInviteLink?> FindActiveInviteAsync(string token, CancellationToken cancellationToken)
    {
        var value = (token ?? string.Empty).Trim();
        if (value.Length < 16)
        {
            return null;
        }

        return await db.RiderInviteLinks
            .Include(x => x.Operator)
            .FirstOrDefaultAsync(x => x.Token == value && x.IsActive && x.Operator.IsActive, cancellationToken);
    }

    private async Task<(string Name, string Phone, string Plate, string Franchise, string? Model, string LicenseType, string LicenseNumber, string? Error)> ParseAsync(
        CreateRiderForm form,
        CancellationToken cancellationToken)
    {
        var name = (form.FullName ?? string.Empty).Trim();
        var phone = PhoneNormalizer.Normalize(form.Phone);
        var plate = (form.PlateNumber ?? string.Empty).Trim().ToUpperInvariant();
        var franchise = (form.VehicleFranchiseNumber ?? string.Empty).Trim().ToUpperInvariant();
        var licenseType = (form.LicenseType ?? string.Empty).Trim();
        var licenseNumber = (form.LicenseNumber ?? string.Empty).Trim();
        if (name.Length == 0 || phone.Length < 10 || plate.Length == 0 || franchise.Length == 0 || licenseType.Length == 0 || licenseNumber.Length == 0)
        {
            return ("", "", "", "", null, "", "", "Name, phone, plate, vehicle franchise number, license type, and license number are required.");
        }

        if (form.VehicleType is not VehicleType.Motorcycle and not VehicleType.Tricycle)
        {
            return ("", "", "", "", null, "", "", "Choose Motorcycle or Tricycle.");
        }

        var taken = await db.Users.AnyAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (taken)
        {
            return ("", "", "", "", null, "", "", "That phone is already in use.");
        }

        var model = string.IsNullOrWhiteSpace(form.VehicleModel) ? null : form.VehicleModel.Trim();
        return (name, phone, plate, franchise, model, licenseType, licenseNumber, null);
    }
}

public record RiderApplicationStatusRequest(string? Phone);

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator")]
public class OperatorRiderInviteController(AppDbContext db) : ControllerBase
{
    [HttpGet("rider-invite")]
    public async Task<ActionResult<RiderInviteLinkResponse>> GetInvite(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var invite = await EnsureInviteAsync(op.Id, cancellationToken);
        return Ok(MapInvite(invite));
    }

    [HttpPost("rider-invite/regenerate")]
    public async Task<ActionResult<RiderInviteLinkResponse>> Regenerate(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var existing = await db.RiderInviteLinks
            .Where(x => x.OperatorId == op.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var row in existing)
        {
            row.IsActive = false;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        var invite = new RiderInviteLink
        {
            OperatorId = op.Id,
            Token = NewToken(),
            IsActive = true
        };
        db.RiderInviteLinks.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapInvite(invite));
    }

    [HttpGet("rider-applications")]
    public async Task<ActionResult<PagedResult<RiderApplicationListItem>>> ListApplications(
        [FromQuery] string? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, httpStatus, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(httpStatus, new { message });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.RiderApplications.Where(x => x.OperatorId == op!.Id);
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<RiderApplicationStatus>(status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var phone = PhoneNormalizer.Normalize(term);
            query = query.Where(x =>
                x.FullName.Contains(term) ||
                x.PhoneNumber.Contains(phone.Length > 0 ? phone : term) ||
                x.PlateNumber.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<RiderApplicationListItem>(
            rows.Select(x => new RiderApplicationListItem(
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.VehicleType.ToString(),
                x.PlateNumber,
                x.Status.ToString(),
                x.CreatedAtUtc)).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("rider-applications/{id:guid}")]
    public async Task<ActionResult<RiderApplicationDetailResponse>> GetApplication(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await LoadApplicationAsync(op.Id, id, cancellationToken);
        return row is null ? NotFound() : Ok(MapDetail(row));
    }

    [HttpPost("rider-applications/{id:guid}/approve")]
    public async Task<ActionResult<RiderApplicationDetailResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var application = await db.RiderApplications
            .Include(x => x.AddressBarangay)
                .ThenInclude(x => x.Municipality)
                    .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (application.Status != RiderApplicationStatus.Pending)
        {
            return BadRequest(new { message = "Only pending applications can be approved." });
        }

        var phoneTaken = await db.Users.AnyAsync(x => x.PhoneNumber == application.PhoneNumber, cancellationToken);
        if (phoneTaken)
        {
            return BadRequest(new { message = "That phone is already in use by another account." });
        }

        var reviewerId = AdminAccess.UserId(User);
        var user = new AppUser
        {
            FullName = application.FullName,
            PhoneNumber = application.PhoneNumber,
            PasswordHash = application.PasswordHash,
            Role = UserRole.Rider,
            OperatorId = op.Id,
            IsActive = true
        };
        var rider = new RiderProfile
        {
            AppUser = user,
            OperatorId = op.Id,
            VehicleType = application.VehicleType,
            PlateNumber = application.PlateNumber,
            VehicleFranchiseNumber = application.VehicleFranchiseNumber,
            VehicleModel = application.VehicleModel,
            LicenseType = application.LicenseType,
            LicenseNumber = application.LicenseNumber,
            IsActive = true,
            AddressBarangayId = application.AddressBarangayId,
            AddressDetails = application.AddressDetails,
            FullAddress = application.FullAddress,
            ProfilePhotoPath = application.ProfilePhotoPath,
            LicensePhotoPath = application.LicensePhotoPath
        };

        db.Users.Add(user);
        db.RiderProfiles.Add(rider);
        await db.SaveChangesAsync(cancellationToken);

        var methods = ParsePaymentMethods(application.AcceptedPaymentMethods);
        var paymentSync = await RiderPaymentSync.SyncAsync(db, rider, methods, cancellationToken);
        if (!paymentSync.Ok)
        {
            return BadRequest(new { message = paymentSync.Error });
        }

        application.Status = RiderApplicationStatus.Approved;
        application.ReviewedAtUtc = DateTime.UtcNow;
        application.ReviewedByUserId = reviewerId;
        application.RiderProfileId = rider.Id;
        application.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadApplicationAsync(op.Id, application.Id, cancellationToken);
        return Ok(MapDetail(loaded!));
    }

    [HttpPost("rider-applications/{id:guid}/reject")]
    public async Task<ActionResult<RiderApplicationDetailResponse>> Reject(
        Guid id,
        [FromBody] RejectRiderApplicationRequest? request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var application = await db.RiderApplications
            .FirstOrDefaultAsync(x => x.OperatorId == op!.Id && x.Id == id, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (application.Status != RiderApplicationStatus.Pending)
        {
            return BadRequest(new { message = "Only pending applications can be rejected." });
        }

        application.Status = RiderApplicationStatus.Rejected;
        application.ReviewNote = string.IsNullOrWhiteSpace(request?.Note) ? null : request!.Note!.Trim();
        application.ReviewedAtUtc = DateTime.UtcNow;
        application.ReviewedByUserId = AdminAccess.UserId(User);
        application.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadApplicationAsync(op.Id, application.Id, cancellationToken);
        return Ok(MapDetail(loaded!));
    }

    private async Task<RiderInviteLink> EnsureInviteAsync(Guid operatorId, CancellationToken cancellationToken)
    {
        var invite = await db.RiderInviteLinks
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.IsActive, cancellationToken);
        if (invite is not null)
        {
            return invite;
        }

        invite = new RiderInviteLink
        {
            OperatorId = operatorId,
            Token = NewToken(),
            IsActive = true
        };
        db.RiderInviteLinks.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    private async Task<RiderApplication?> LoadApplicationAsync(Guid operatorId, Guid id, CancellationToken cancellationToken) =>
        await db.RiderApplications
            .AsNoTracking()
            .Include(x => x.AddressBarangay)
                .ThenInclude(x => x!.Municipality)
                    .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.Id == id, cancellationToken);

    private static RiderInviteLinkResponse MapInvite(RiderInviteLink invite) =>
        new(
            invite.Token,
            PublicRiderInviteController.JoinPathPrefix + invite.Token,
            PublicRiderInviteController.StatusPath,
            invite.CreatedAtUtc);

    private static RiderApplicationDetailResponse MapDetail(RiderApplication row)
    {
        var methods = ParsePaymentMethods(row.AcceptedPaymentMethods)
            .Select(x => x.ToString())
            .ToList();
        return new RiderApplicationDetailResponse(
            row.Id,
            row.FullName,
            row.PhoneNumber,
            row.VehicleType.ToString(),
            row.PlateNumber,
            row.VehicleFranchiseNumber,
            row.VehicleModel,
            row.LicenseType,
            row.LicenseNumber,
            row.Status.ToString(),
            row.FullAddress,
            OperatorAddressSync.Map(
                new RiderProfile
                {
                    AddressBarangayId = row.AddressBarangayId,
                    AddressDetails = row.AddressDetails,
                    FullAddress = row.FullAddress,
                    AddressBarangay = row.AddressBarangay
                }),
            methods,
            UploadUrls.FromPath(row.ProfilePhotoPath),
            UploadUrls.FromPath(row.LicensePhotoPath),
            row.ReviewNote,
            row.CreatedAtUtc,
            row.ReviewedAtUtc,
            row.RiderProfileId);
    }

    private static List<PaymentMethod> ParsePaymentMethods(string raw)
    {
        var list = new List<PaymentMethod>();
        foreach (var part in (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var number) && Enum.IsDefined(typeof(PaymentMethod), number))
            {
                list.Add((PaymentMethod)number);
            }
            else if (Enum.TryParse<PaymentMethod>(part, true, out var named))
            {
                list.Add(named);
            }
        }

        return list.Distinct().ToList();
    }

    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record RejectRiderApplicationRequest(string? Note);
