using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/reports")]
public class OperatorReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("commission")]
    public async Task<ActionResult<CommissionReportResponse>> Commission(
        [FromQuery] string? bookingNo,
        [FromQuery] string? rider,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await CommissionReport.BuildAsync(
            db,
            op.Id,
            bookingNo,
            rider,
            from,
            to,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("riders")]
    public async Task<ActionResult<RiderReportResponse>> Riders(
        [FromQuery] string? q,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await RiderReport.BuildAsync(db, op.Id, q, from, to, page, pageSize, cancellationToken));
    }

    [HttpGet("riders/export")]
    public async Task<IActionResult> ExportRiders(
        [FromQuery] string? q,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await RiderReport.BuildRowsAsync(
            db,
            op.Id,
            q,
            from,
            to,
            RiderReport.ExportMaxRows,
            cancellationToken);
        var bytes = RiderReportExcel.Build(rows);
        var stamp = DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd");
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"rider-report-{stamp}.xlsx");
    }

    [HttpGet("customers")]
    public async Task<ActionResult<CustomerReportResponse>> Customers(
        [FromQuery] string? q,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        return Ok(await CustomerReport.BuildAsync(db, op.Id, q, from, to, page, pageSize, cancellationToken));
    }

    [HttpGet("customers/export")]
    public async Task<IActionResult> ExportCustomers(
        [FromQuery] string? q,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await CustomerReport.BuildRowsAsync(
            db,
            op.Id,
            q,
            from,
            to,
            CustomerReport.ExportMaxRows,
            cancellationToken);
        var bytes = CustomerReportExcel.Build(rows);
        var stamp = DateTime.UtcNow.AddHours(8).ToString("yyyyMMdd");
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"customer-report-{stamp}.xlsx");
    }
}
