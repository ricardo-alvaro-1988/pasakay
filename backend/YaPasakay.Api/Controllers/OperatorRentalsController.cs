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
[Route("api/operator/rentals")]
public class OperatorRentalsController(AppDbContext db) : ControllerBase
{
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
        DateTime CreatedAtUtc);

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

    public record RentalsPageDto(
        IReadOnlyList<RentalInquiryDto> Inquiries,
        IReadOnlyList<CarListingDto> Listings,
        int PendingInquiries,
        int PendingListings);

    [HttpGet]
    public async Task<ActionResult<RentalsPageDto>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        RentalLeadStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RentalLeadStatus>(status, true, out var parsed))
        {
            filter = parsed;
        }

        var inquiryQuery = db.CustomerRentalInquiries.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(c => c.AppUser)
            .Where(x => x.OperatorId == op.Id);
        var listingQuery = db.CustomerCarListings.AsNoTracking()
            .Include(x => x.Customer).ThenInclude(c => c.AppUser)
            .Where(x => x.OperatorId == op.Id);

        if (filter is not null)
        {
            inquiryQuery = inquiryQuery.Where(x => x.Status == filter);
            listingQuery = listingQuery.Where(x => x.Status == filter);
        }

        var inquiries = await inquiryQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var listings = await listingQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var pendingInquiries = await db.CustomerRentalInquiries.CountAsync(
            x => x.OperatorId == op.Id && x.Status == RentalLeadStatus.Pending, cancellationToken);
        var pendingListings = await db.CustomerCarListings.CountAsync(
            x => x.OperatorId == op.Id && x.Status == RentalLeadStatus.Pending, cancellationToken);

        return Ok(new RentalsPageDto(
            inquiries.Select(x => new RentalInquiryDto(
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
                x.CreatedAtUtc)).ToList(),
            listings.Select(x => new CarListingDto(
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
                x.CreatedAtUtc)).ToList(),
            pendingInquiries,
            pendingListings));
    }

    [HttpPost("inquiries/{id:guid}/status")]
    public async Task<ActionResult> SetInquiryStatus(Guid id, [FromBody] StatusBody body, CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        if (!Enum.TryParse<RentalLeadStatus>(body.Status, true, out var next)
            || next is not (RentalLeadStatus.Pending or RentalLeadStatus.Contacted or RentalLeadStatus.Closed))
        {
            return BadRequest(new { message = "Status must be Pending, Contacted, or Closed." });
        }

        var row = await db.CustomerRentalInquiries.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Rental request not found." });
        }

        row.Status = next;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id = row.Id, status = row.Status.ToString() });
    }

    [HttpPost("listings/{id:guid}/status")]
    public async Task<ActionResult> SetListingStatus(Guid id, [FromBody] StatusBody body, CancellationToken cancellationToken)
    {
        var (op, code, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(code, new { message });
        }

        if (!Enum.TryParse<RentalLeadStatus>(body.Status, true, out var next)
            || next is not (RentalLeadStatus.Pending or RentalLeadStatus.Contacted or RentalLeadStatus.Closed))
        {
            return BadRequest(new { message = "Status must be Pending, Contacted, or Closed." });
        }

        var row = await db.CustomerCarListings.FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
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

    private static IReadOnlyList<string> Days(Domain.Entities.CustomerCarListing x)
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
