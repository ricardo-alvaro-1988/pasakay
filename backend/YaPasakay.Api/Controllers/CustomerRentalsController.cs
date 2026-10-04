using System.Globalization;
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
[Authorize(Roles = "Customer")]
[Route("api/customer/rentals")]
public class CustomerRentalsController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    private static readonly TimeSpan PhilippinesOffset = TimeSpan.FromHours(8);

    public record VehicleOptionDto(string VehicleType, string Name, string IconKey, int MaxPassengers);

    public record SubmitResultDto(Guid Id, string Message);

    [HttpGet("vehicles")]
    public ActionResult<IReadOnlyList<VehicleOptionDto>> Vehicles()
    {
        var rows = VehicleCatalog.PlatformPresets
            .Where(x => x.LegacyEnum != VehicleType.Custom)
            .OrderBy(x => x.SortOrder)
            .Select(x => new VehicleOptionDto(x.LegacyEnum.ToString(), x.Name, x.IconKey, x.MaxPassengers))
            .ToList();
        return Ok(rows);
    }

    [HttpPost("inquire")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<SubmitResultDto>> Inquire(
        [FromForm] string vehicleType,
        [FromForm] string scheduleFrom,
        [FromForm] string scheduleTo,
        [FromForm] string locationDetails,
        [FromForm] double locationLat,
        [FromForm] double locationLng,
        [FromForm] string? notes,
        [FromForm] string mobileNumber,
        [FromForm] Guid? barangayId,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (!VehicleCatalog.TryParseType(vehicleType, out var type) || type == VehicleType.Custom)
        {
            return BadRequest(new { message = "Pick a vehicle type." });
        }

        if (!TryParseLocalDateTime(scheduleFrom, out var fromLocal) || !TryParseLocalDateTime(scheduleTo, out var toLocal))
        {
            return BadRequest(new { message = "Choose a valid schedule From and To date/time." });
        }

        if (toLocal < fromLocal)
        {
            return BadRequest(new { message = "Schedule To must be on or after From." });
        }

        // 30-minute grace: form defaults to now+1d; users often submit several minutes later.
        var minFromPh = DateTime.UtcNow.Add(PhilippinesOffset).AddDays(1).AddMinutes(-30);
        if (fromLocal < minFromPh)
        {
            return BadRequest(new { message = "Schedule From must be at least 1 day from now." });
        }

        var details = (locationDetails ?? string.Empty).Trim();
        if (details.Length is < 3 or > 400)
        {
            return BadRequest(new { message = "Set a location on the map." });
        }

        if (locationLat is < -90 or > 90 || locationLng is < -180 or > 180)
        {
            return BadRequest(new { message = "Pin a valid location on the map." });
        }

        var mobile = NormalizeMobile(mobileNumber);
        if (mobile is null)
        {
            return BadRequest(new { message = "Enter a valid mobile number." });
        }

        var cleanedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (cleanedNotes is { Length: > 1000 })
        {
            return BadRequest(new { message = "Notes must be at most 1000 characters." });
        }

        var barangay = await TerritoryLookup.MatchFromAddressAsync(db, barangayId, details, cancellationToken);
        var op = await ResolveOperatorAsync(barangay, cancellationToken);
        if (op is null)
        {
            return BadRequest(new { message = "No operator covers this location yet. Try another pin nearby." });
        }

        if (!op.RentalEnabled)
        {
            return BadRequest(new { message = "Rental is not available in this area yet." });
        }

        var row = new CustomerRentalInquiry
        {
            OperatorId = op.Id,
            CustomerId = customer.Id,
            VehicleType = type,
            VehicleCategoryId = VehicleCatalog.IdFor(type),
            ScheduleFromUtc = ToUtcFromPhLocal(fromLocal),
            ScheduleToUtc = ToUtcFromPhLocal(toLocal),
            LocationDetails = details,
            LocationLat = locationLat,
            LocationLng = locationLng,
            BarangayId = barangay?.Id,
            Notes = cleanedNotes,
            MobileNumber = mobile,
            Status = RentalLeadStatus.Pending,
        };
        db.CustomerRentalInquiries.Add(row);

        var customerName = customer.AppUser?.FullName ?? "Customer";
        db.OperatorNotifications.Add(new OperatorNotification
        {
            OperatorId = op.Id,
            Kind = NotificationKind.Rental,
            Title = "New rental request",
            Body = Truncate($"{customerName} wants a {type} from {fromLocal:MMM d} to {toLocal:MMM d} · {details}"),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new SubmitResultDto(row.Id, "We will come back to you soonest."));
    }

    [HttpPost("list-car")]
    [RequestSizeLimit(UploadStore.MaxImageBytes * 5 + 2_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadStore.MaxImageBytes * 5 + 2_000_000)]
    public async Task<ActionResult<SubmitResultDto>> ListCar(
        [FromForm] string vehicleType,
        [FromForm] string plate,
        [FromForm] int seater,
        [FromForm] IFormFile? front,
        [FromForm] IFormFile? back,
        [FromForm] IFormFile? left,
        [FromForm] IFormFile? right,
        [FromForm] IFormFile? inside,
        [FromForm] bool availableMonday = true,
        [FromForm] bool availableTuesday = true,
        [FromForm] bool availableWednesday = true,
        [FromForm] bool availableThursday = true,
        [FromForm] bool availableFriday = true,
        [FromForm] bool availableSaturday = true,
        [FromForm] bool availableSunday = true,
        [FromForm] double? locationLat = null,
        [FromForm] double? locationLng = null,
        [FromForm] string? locationDetails = null,
        [FromForm] Guid? barangayId = null,
        CancellationToken cancellationToken = default)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (!VehicleCatalog.TryParseType(vehicleType, out var type) || type == VehicleType.Custom)
        {
            return BadRequest(new { message = "Pick a vehicle type." });
        }

        var plateNumber = (plate ?? string.Empty).Trim().ToUpperInvariant();
        if (plateNumber.Length is < 3 or > 40)
        {
            return BadRequest(new { message = "Enter a plate number." });
        }

        var preset = VehicleCatalog.PresetFor(type);
        var maxSeats = preset?.MaxPassengers ?? 12;
        if (seater < 1 || seater > maxSeats)
        {
            return BadRequest(new { message = $"Seater must be between 1 and {maxSeats}." });
        }

        if (front is null || back is null || left is null || right is null || inside is null
            || front.Length == 0 || back.Length == 0 || left.Length == 0 || right.Length == 0 || inside.Length == 0)
        {
            return BadRequest(new { message = "Upload all 5 photos: Front, Back, Left, Right, and Inside." });
        }

        if (!(availableMonday || availableTuesday || availableWednesday || availableThursday
              || availableFriday || availableSaturday || availableSunday))
        {
            return BadRequest(new { message = "Pick at least one available day." });
        }

        var barangay = await TerritoryLookup.MatchFromAddressAsync(db, barangayId, locationDetails, cancellationToken);
        var op = await ResolveOperatorAsync(barangay, cancellationToken);
        if (op is null)
        {
            return BadRequest(new { message = "No operator in your area yet. Turn on location or try again later." });
        }

        if (!op.RentalEnabled)
        {
            return BadRequest(new { message = "Rental is not available in this area yet." });
        }

        var row = new CustomerCarListing
        {
            OperatorId = op.Id,
            CustomerId = customer.Id,
            VehicleType = type,
            VehicleCategoryId = VehicleCatalog.IdFor(type),
            PlateNumber = plateNumber,
            Seater = seater,
            AvailableMonday = availableMonday,
            AvailableTuesday = availableTuesday,
            AvailableWednesday = availableWednesday,
            AvailableThursday = availableThursday,
            AvailableFriday = availableFriday,
            AvailableSaturday = availableSaturday,
            AvailableSunday = availableSunday,
            Status = RentalLeadStatus.Pending,
            FrontImagePath = string.Empty,
            BackImagePath = string.Empty,
            LeftImagePath = string.Empty,
            RightImagePath = string.Empty,
            InsideImagePath = string.Empty,
        };

        try
        {
            var folder = $"rental-listings/{row.Id}";
            row.FrontImagePath = await uploads.SaveAsync(front, folder, "front", cancellationToken)
                ?? throw new InvalidOperationException("Could not save front photo.");
            row.BackImagePath = await uploads.SaveAsync(back, folder, "back", cancellationToken)
                ?? throw new InvalidOperationException("Could not save back photo.");
            row.LeftImagePath = await uploads.SaveAsync(left, folder, "left", cancellationToken)
                ?? throw new InvalidOperationException("Could not save left photo.");
            row.RightImagePath = await uploads.SaveAsync(right, folder, "right", cancellationToken)
                ?? throw new InvalidOperationException("Could not save right photo.");
            row.InsideImagePath = await uploads.SaveAsync(inside, folder, "inside", cancellationToken)
                ?? throw new InvalidOperationException("Could not save inside photo.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        db.CustomerCarListings.Add(row);
        var customerName = customer.AppUser?.FullName ?? "Customer";
        db.OperatorNotifications.Add(new OperatorNotification
        {
            OperatorId = op.Id,
            Kind = NotificationKind.Rental,
            Title = "New car listing",
            Body = Truncate($"{customerName} listed a {type} · {plateNumber} · {seater} seater"),
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new SubmitResultDto(row.Id, "We will come back to you soonest."));
    }

    private async Task<Operator?> ResolveOperatorAsync(Barangay? barangay, CancellationToken cancellationToken)
    {
        if (barangay is null)
        {
            return null;
        }

        return await db.Operators.AsNoTracking()
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.BarangayId == barangay.Id)
                || x.Areas.Any(a => a.Barangay.MunicipalityId == barangay.MunicipalityId)))
            .OrderBy(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool TryParseLocalDateTime(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // datetime-local: 2026-10-05T14:30
        if (DateTime.TryParseExact(
                value.Trim(),
                ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)
               || DateTime.TryParse(value, out date);
    }

    private static DateTime ToUtcFromPhLocal(DateTime phLocal) =>
        DateTime.SpecifyKind(phLocal - PhilippinesOffset, DateTimeKind.Utc);

    private static string? NormalizeMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("63") && digits.Length >= 12) digits = "0" + digits[2..];
        if (digits.Length is < 10 or > 13) return null;
        return digits.Length == 10 && !digits.StartsWith('0') ? "0" + digits : digits;
    }

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..397] + "...";
}
