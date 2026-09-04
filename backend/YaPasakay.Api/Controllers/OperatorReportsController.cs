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
}
