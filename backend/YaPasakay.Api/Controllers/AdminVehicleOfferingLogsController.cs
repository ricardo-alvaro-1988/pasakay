using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/vehicle-offering-logs")]
public class AdminVehicleOfferingLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet("terms")]
    public ActionResult<VehicleOfferingTermsInfo> Terms() =>
        Ok(new VehicleOfferingTermsInfo(VehicleOfferingTerms.Version, VehicleOfferingTerms.Text));

    [HttpGet]
    public async Task<ActionResult<PagedResult<VehicleOfferingLogItem>>> List(
        [FromQuery] Guid? operatorId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.VehicleOfferingLogs.AsNoTracking().AsQueryable();
        if (operatorId is Guid opId)
        {
            query = query.Where(x => x.OperatorId == opId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.Operator.CompanyName.Contains(term)
                || x.VehicleName.Contains(term)
                || x.VehicleCode.Contains(term)
                || x.ActorName.Contains(term)
                || (x.Municipality != null && x.Municipality.Name.Contains(term)));
        }

        query = query.OrderByDescending(x => x.AtUtc);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VehicleOfferingLogItem(
                x.Id,
                x.OperatorId,
                x.Operator.CompanyName,
                x.MunicipalityId,
                x.Municipality != null ? x.Municipality.Name : null,
                x.VehicleCategoryId,
                x.VehicleCode,
                x.VehicleName,
                x.VehicleType.ToString(),
                x.IsOffered,
                x.ActorName,
                x.ActorRole,
                x.AcceptedTerms,
                x.TermsVersion,
                x.AtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<VehicleOfferingLogItem>(rows, page, pageSize, total));
    }
}
