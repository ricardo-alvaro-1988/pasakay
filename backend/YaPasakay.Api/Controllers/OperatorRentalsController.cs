using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/rentals")]
public class OperatorRentalsController(AppDbContext db) : ControllerBase
{
    private const int MaxMatchRangeDays = 90;
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;

    public record RentalInquiryDto(
        Guid Id,
        string CustomerName,
        string MobileNumber,
        string VehicleType,
        DateTime ScheduleFromUtc,
        DateTime ScheduleToUtc,
        string LocationDetails,
        double LocationLat,
        double LocationLng,
        string? Notes,
        string Status,
        DateTime CreatedAtUtc,
        Guid? MatchedListingId,
        string? MatchedPlate,
        DateTime? MatchedAtUtc);

    public record CarListingDto(
        Guid Id,
        string CustomerName,
        string MobileNumber,
        string VehicleType,
        string PlateNumber,
        int Seater,
        string? FrontImageUrl,
        string? BackImageUrl,
        string? LeftImageUrl,
        string? RightImageUrl,
        string? InsideImageUrl,
        IReadOnlyList<string> AvailableDays,
        string Status,
        DateTime CreatedAtUtc);

    public record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    public record RentalsSummaryDto(int PendingInquiries, int PendingListings);

    public record MatchBody(Guid ListingId);

    private async Task<(Operator? Op, ActionResult? Error)> RequireRentalOperatorAsync(CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return (null, StatusCode(code, new { message }));
        }

        if (!op.RentalEnabled)
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { message = "Rental is off for this operator. Ask admin to activate it." }));
        }

        return (op, null);
    }

    /// <summary>Legacy combined list (kept for older clients).</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        var inquiries = await ListInquiriesCore(op!.Id, null, status, 1, 100, cancellationToken);
        var listings = await ListListingsCore(op.Id, null, status, 1, 100, cancellationToken);
        var summary = await SummaryCore(op.Id, cancellationToken);
        return Ok(new
        {
            inquiries = inquiries.Items,
            listings = listings.Items,
            pendingInquiries = summary.PendingInquiries,
            pendingListings = summary.PendingListings,
        });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<RentalsSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        return Ok(await SummaryCore(op!.Id, cancellationToken));
    }

    [HttpGet("inquiries")]
    public async Task<ActionResult<PagedResultDto<RentalInquiryDto>>> ListInquiries(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        return Ok(await ListInquiriesCore(op!.Id, q, status, page, pageSize, cancellationToken));
    }

    [HttpGet("listings")]
    public async Task<ActionResult<PagedResultDto<CarListingDto>>> ListListings(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        return Ok(await ListListingsCore(op!.Id, q, status, page, pageSize, cancellationToken));
    }

    [HttpGet("inquiries/{id:guid}/matches")]
    public async Task<ActionResult<IReadOnlyList<CarListingDto>>> SuggestMatches(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;
        var operatorId = op!.Id;

        var inquiry = await db.CustomerRentalInquiries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);
        if (inquiry is null)
        {
            return NotFound(new { message = "Rental request not found." });
        }

        if (inquiry.Status is RentalLeadStatus.Closed or RentalLeadStatus.Matched)
        {
            return Ok(Array.Empty<CarListingDto>());
        }

        var candidates = await db.CustomerCarListings.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(c => c.AppUser)
            .Where(x => x.OperatorId == operatorId
                && x.VehicleType == inquiry.VehicleType
                && (x.Status == RentalLeadStatus.Pending || x.Status == RentalLeadStatus.Contacted))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var matchedListingIds = await db.CustomerRentalInquiries.AsNoTracking()
            .Where(x => x.OperatorId == operatorId && x.MatchedListingId != null)
            .Select(x => x.MatchedListingId!.Value)
            .ToListAsync(cancellationToken);
        var taken = matchedListingIds.ToHashSet();

        var matches = candidates
            .Where(x => !taken.Contains(x.Id) && AvailabilityCovers(x, inquiry.ScheduleFromUtc, inquiry.ScheduleToUtc))
            .Select(ToListingDto)
            .ToList();

        return Ok(matches);
    }

    [HttpPost("inquiries/{id:guid}/match")]
    public async Task<ActionResult> AssignMatch(
        Guid id,
        [FromBody] MatchBody body,
        CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        var inquiry = await db.CustomerRentalInquiries
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op!.Id, cancellationToken);
        if (inquiry is null)
        {
            return NotFound(new { message = "Rental request not found." });
        }

        if (inquiry.Status == RentalLeadStatus.Closed)
        {
            return BadRequest(new { message = "This rental request is closed." });
        }

        if (inquiry.MatchedListingId is not null || inquiry.Status == RentalLeadStatus.Matched)
        {
            return BadRequest(new { message = "This rental request already has an assigned vehicle." });
        }

        var listing = await db.CustomerCarListings
            .FirstOrDefaultAsync(x => x.Id == body.ListingId && x.OperatorId == op!.Id, cancellationToken);
        if (listing is null)
        {
            return NotFound(new { message = "Car listing not found." });
        }

        if (listing.Status is RentalLeadStatus.Matched or RentalLeadStatus.Closed)
        {
            return BadRequest(new { message = "That vehicle is not available to assign." });
        }

        if (listing.VehicleType != inquiry.VehicleType)
        {
            return BadRequest(new { message = "Vehicle type does not match the request." });
        }

        if (!AvailabilityCovers(listing, inquiry.ScheduleFromUtc, inquiry.ScheduleToUtc))
        {
            return BadRequest(new { message = "Listing availability does not cover the request schedule." });
        }

        var alreadyTaken = await db.CustomerRentalInquiries.AnyAsync(
            x => x.MatchedListingId == listing.Id && x.Id != inquiry.Id, cancellationToken);
        if (alreadyTaken)
        {
            return BadRequest(new { message = "That vehicle is already assigned to another request." });
        }

        var now = DateTime.UtcNow;
        inquiry.MatchedListingId = listing.Id;
        inquiry.MatchedAtUtc = now;
        inquiry.Status = RentalLeadStatus.Matched;
        inquiry.UpdatedAtUtc = now;
        listing.Status = RentalLeadStatus.Matched;
        listing.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            inquiryId = inquiry.Id,
            listingId = listing.Id,
            plateNumber = listing.PlateNumber,
            status = inquiry.Status.ToString(),
            matchedAtUtc = inquiry.MatchedAtUtc,
        });
    }

    [HttpPost("inquiries/{id:guid}/status")]
    public async Task<ActionResult> SetInquiryStatus(Guid id, [FromBody] StatusBody body, CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        if (!TryParseLeadStatus(body.Status, out var next)
            || next is not (RentalLeadStatus.Pending or RentalLeadStatus.Contacted or RentalLeadStatus.Closed or RentalLeadStatus.Matched))
        {
            return BadRequest(new { message = "Status must be Pending, Contacted, Matched, or Closed." });
        }

        var row = await db.CustomerRentalInquiries.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Rental request not found." });
        }

        if (next == RentalLeadStatus.Matched && row.MatchedListingId is null)
        {
            return BadRequest(new { message = "Assign a vehicle before marking as Matched." });
        }

        row.Status = next;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id = row.Id, status = row.Status.ToString() });
    }

    [HttpPost("listings/{id:guid}/status")]
    public async Task<ActionResult> SetListingStatus(Guid id, [FromBody] StatusBody body, CancellationToken cancellationToken)
    {
        var (op, error) = await RequireRentalOperatorAsync(cancellationToken);
        if (error is not null) return error;

        if (!TryParseLeadStatus(body.Status, out var next)
            || next is not (RentalLeadStatus.Pending or RentalLeadStatus.Contacted or RentalLeadStatus.Closed or RentalLeadStatus.Matched))
        {
            return BadRequest(new { message = "Status must be Pending, Contacted, Matched, or Closed." });
        }

        var row = await db.CustomerCarListings.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Car listing not found." });
        }

        row.Status = next;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id = row.Id, status = row.Status.ToString() });
    }

    public record StatusBody(string Status);

    private async Task<RentalsSummaryDto> SummaryCore(Guid operatorId, CancellationToken cancellationToken)
    {
        var pendingInquiries = await db.CustomerRentalInquiries.CountAsync(
            x => x.OperatorId == operatorId && x.Status == RentalLeadStatus.Pending, cancellationToken);
        var pendingListings = await db.CustomerCarListings.CountAsync(
            x => x.OperatorId == operatorId && x.Status == RentalLeadStatus.Pending, cancellationToken);
        return new RentalsSummaryDto(pendingInquiries, pendingListings);
    }

    private async Task<PagedResultDto<RentalInquiryDto>> ListInquiriesCore(
        Guid operatorId,
        string? q,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.CustomerRentalInquiries.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(c => c.AppUser)
            .Include(x => x.MatchedListing)
            .Where(x => x.OperatorId == operatorId);

        if (TryParseLeadStatus(status, out var filter))
        {
            query = query.Where(x => x.Status == filter);
        }

        var term = q?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var like = $"%{term}%";
            VehicleType? vehicleHit = Enum.TryParse<VehicleType>(term, true, out var vt) ? vt : null;
            query = query.Where(x =>
                EF.Functions.Like(x.Customer.AppUser.FullName, like)
                || EF.Functions.Like(x.MobileNumber, like)
                || EF.Functions.Like(x.LocationDetails, like)
                || (x.Notes != null && EF.Functions.Like(x.Notes, like))
                || (vehicleHit != null && x.VehicleType == vehicleHit)
                || (x.MatchedListing != null && EF.Functions.Like(x.MatchedListing.PlateNumber, like)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<RentalInquiryDto>(
            rows.Select(ToInquiryDto).ToList(),
            total,
            page,
            pageSize);
    }

    private async Task<PagedResultDto<CarListingDto>> ListListingsCore(
        Guid operatorId,
        string? q,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.CustomerCarListings.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(c => c.AppUser)
            .Where(x => x.OperatorId == operatorId);

        if (TryParseLeadStatus(status, out var filter))
        {
            query = query.Where(x => x.Status == filter);
        }

        var term = q?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var like = $"%{term}%";
            VehicleType? vehicleHit = Enum.TryParse<VehicleType>(term, true, out var vt) ? vt : null;
            query = query.Where(x =>
                EF.Functions.Like(x.Customer.AppUser.FullName, like)
                || EF.Functions.Like(x.Customer.AppUser.PhoneNumber, like)
                || EF.Functions.Like(x.PlateNumber, like)
                || (vehicleHit != null && x.VehicleType == vehicleHit));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<CarListingDto>(
            rows.Select(ToListingDto).ToList(),
            total,
            page,
            pageSize);
    }

    private static RentalInquiryDto ToInquiryDto(CustomerRentalInquiry x) =>
        new(
            x.Id,
            x.Customer.AppUser.FullName,
            x.MobileNumber,
            x.VehicleType.ToString(),
            x.ScheduleFromUtc,
            x.ScheduleToUtc,
            x.LocationDetails,
            x.LocationLat,
            x.LocationLng,
            x.Notes,
            x.Status.ToString(),
            x.CreatedAtUtc,
            x.MatchedListingId,
            x.MatchedListing?.PlateNumber,
            x.MatchedAtUtc);

    private static CarListingDto ToListingDto(CustomerCarListing x) =>
        new(
            x.Id,
            x.Customer.AppUser.FullName,
            x.Customer.AppUser.PhoneNumber,
            x.VehicleType.ToString(),
            x.PlateNumber,
            x.Seater,
            UploadUrls.FromPath(x.FrontImagePath),
            UploadUrls.FromPath(x.BackImagePath),
            UploadUrls.FromPath(x.LeftImagePath),
            UploadUrls.FromPath(x.RightImagePath),
            UploadUrls.FromPath(x.InsideImagePath),
            Days(x),
            x.Status.ToString(),
            x.CreatedAtUtc);

    private static bool TryParseLeadStatus(string? status, out RentalLeadStatus parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status, true, out parsed);
    }

    private static bool AvailabilityCovers(CustomerCarListing listing, DateTime fromUtc, DateTime toUtc)
    {
        var fromPh = PhilippineTime.ToPh(fromUtc).Date;
        var toPh = PhilippineTime.ToPh(toUtc).Date;
        if (toPh < fromPh)
        {
            return false;
        }

        var spanDays = (toPh - fromPh).TotalDays;
        if (spanDays > MaxMatchRangeDays)
        {
            return false;
        }

        for (var day = fromPh; day <= toPh; day = day.AddDays(1))
        {
            if (!DayAvailable(listing, day.DayOfWeek))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DayAvailable(CustomerCarListing listing, DayOfWeek day) =>
        day switch
        {
            DayOfWeek.Monday => listing.AvailableMonday,
            DayOfWeek.Tuesday => listing.AvailableTuesday,
            DayOfWeek.Wednesday => listing.AvailableWednesday,
            DayOfWeek.Thursday => listing.AvailableThursday,
            DayOfWeek.Friday => listing.AvailableFriday,
            DayOfWeek.Saturday => listing.AvailableSaturday,
            DayOfWeek.Sunday => listing.AvailableSunday,
            _ => false,
        };

    private static IReadOnlyList<string> Days(CustomerCarListing x)
    {
        var list = new List<string>(7);
        if (x.AvailableMonday) list.Add("Mon");
        if (x.AvailableTuesday) list.Add("Tue");
        if (x.AvailableWednesday) list.Add("Wed");
        if (x.AvailableThursday) list.Add("Thu");
        if (x.AvailableFriday) list.Add("Fri");
        if (x.AvailableSaturday) list.Add("Sat");
        if (x.AvailableSunday) list.Add("Sun");
        return list;
    }
}
