using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reports")]
public class AdminReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("commission")]
    public async Task<ActionResult<CommissionReportResponse>> Commission(
        [FromQuery] Guid? operatorId,
        [FromQuery] string? bookingNo,
        [FromQuery] string? rider,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await CommissionReport.BuildAsync(
            db,
            operatorId,
            bookingNo,
            rider,
            from,
            to,
            page,
            pageSize,
            cancellationToken));
    }
}
