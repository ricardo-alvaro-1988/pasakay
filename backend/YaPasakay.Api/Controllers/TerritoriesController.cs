using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YaPasakay.Application.Admin;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Route("api/territories")]
public class TerritoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet("provinces")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<IdName>>> Provinces(CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.ProvincesAsync(db, cancellationToken));

    [HttpGet("municipalities")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<IdName>>> Municipalities(
        [FromQuery] Guid provinceId,
        CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.MunicipalitiesAsync(db, provinceId, cancellationToken));

    [HttpGet("barangays")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BarangayOption>>> Barangays(
        [FromQuery] Guid municipalityId,
        CancellationToken cancellationToken) =>
        Ok(await TerritoryLookup.BarangaysAsync(db, municipalityId, cancellationToken));
}
