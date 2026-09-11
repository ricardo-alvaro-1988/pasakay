using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/pabili-surcharges")]
public class OperatorPabiliSurchargesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PabiliSurchargeListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var matrix = await EnsureMatrixAsync(op, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildAsync(op, matrix.Id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PabiliSurchargeListResponse>> Create(
        [FromBody] SaveFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var (parsed, error) = ParseSurcharge(request);
        if (parsed is null)
        {
            return BadRequest(new { message = error });
        }

        var matrix = await EnsureMatrixAsync(op, cancellationToken);
        parsed.PabiliMatrixId = matrix.Id;
        db.PabiliSurcharges.Add(parsed);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildAsync(op, matrix.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PabiliSurchargeListResponse>> Update(
        Guid id,
        [FromBody] SaveFareSurchargeRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var matrix = await EnsureMatrixAsync(op, cancellationToken);
        var row = await db.PabiliSurcharges
            .FirstOrDefaultAsync(x => x.Id == id && x.PabiliMatrixId == matrix.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Surcharge not found." });
        }

        var (parsed, error) = ParseSurcharge(request);
        if (parsed is null)
        {
            return BadRequest(new { message = error });
        }

        row.Kind = parsed.Kind;
        row.Name = parsed.Name;
        row.Amount = parsed.Amount;
        row.IsActive = parsed.IsActive;
        row.WindowStart = parsed.WindowStart;
        row.WindowEnd = parsed.WindowEnd;
        row.RangeStartUtc = parsed.RangeStartUtc;
        row.RangeEndUtc = parsed.RangeEndUtc;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildAsync(op, matrix.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<ActionResult<PabiliSurchargeListResponse>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var matrix = await EnsureMatrixAsync(op, cancellationToken);
        var row = await db.PabiliSurcharges
            .FirstOrDefaultAsync(x => x.Id == id && x.PabiliMatrixId == matrix.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Surcharge not found." });
        }

        db.PabiliSurcharges.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildAsync(op, matrix.Id, cancellationToken));
    }

    private async Task<PabiliMatrix> EnsureMatrixAsync(Operator op, CancellationToken cancellationToken)
    {
        var matrix = await db.PabiliMatrices
            .FirstOrDefaultAsync(x => x.OperatorId == op.Id, cancellationToken);
        if (matrix is not null)
        {
            return matrix;
        }

        matrix = new PabiliMatrix
        {
            OperatorId = op.Id,
            BaseFareAmount = 0,
            KmScope = 1,
            SucceedingKm = 0,
            IsActive = true,
        };
        FareCommissionSplit.ApplyDefaults(
            matrix,
            op.PabiliFareSystemCommissionPercent,
            op.PabiliMarkupSystemCommissionPercent);
        db.PabiliMatrices.Add(matrix);
        return matrix;
    }

    private async Task<PabiliSurchargeListResponse> BuildAsync(
        Operator op,
        Guid matrixId,
        CancellationToken cancellationToken)
    {
        var items = await db.PabiliSurcharges
            .AsNoTracking()
            .Where(x => x.PabiliMatrixId == matrixId)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .Select(x => new FareSurchargeItem(
                x.Id,
                x.Kind,
                x.Name,
                x.Amount,
                x.WindowStart.HasValue ? x.WindowStart.Value.ToString("HH\\:mm") : null,
                x.WindowEnd.HasValue ? x.WindowEnd.Value.ToString("HH\\:mm") : null,
                x.RangeStartUtc.HasValue
                    ? DateTime.SpecifyKind(x.RangeStartUtc.Value, DateTimeKind.Utc)
                    : null,
                x.RangeEndUtc.HasValue
                    ? DateTime.SpecifyKind(x.RangeEndUtc.Value, DateTimeKind.Utc)
                    : null,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PabiliSurchargeListResponse(op.Id, op.CompanyName, items);
    }

    private static (PabiliSurcharge? Item, string? Error) ParseSurcharge(SaveFareSurchargeRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || request.Amount < 0)
        {
            return (null, "Surcharge name and a non-negative amount are required.");
        }

        var item = new PabiliSurcharge
        {
            Kind = request.Kind,
            Name = name.Length > 80 ? name[..80] : name,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            IsActive = request.IsActive
        };

        if (request.Kind == SurchargeKind.TimeWindow)
        {
            if (!TryParseWindowTime(request.WindowStart, out var start)
                || !TryParseWindowTime(request.WindowEnd, out var end))
            {
                return (null, "Window surcharges need a start and end time in Philippine time.");
            }

            item.WindowStart = start;
            item.WindowEnd = end;
            return (item, null);
        }

        if (request.Kind != SurchargeKind.DateRange)
        {
            return (null, "Choose a window surcharge or a date range.");
        }

        if (request.RangeStartUtc is null || request.RangeEndUtc is null)
        {
            return (null, "Date-range surcharges need a start and end.");
        }

        var from = DateTime.SpecifyKind(request.RangeStartUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.RangeEndUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        item.RangeStartUtc = from;
        item.RangeEndUtc = to;
        return (item, null);
    }

    private static bool TryParseWindowTime(string? value, out TimeOnly time)
    {
        time = default;
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return false;
        }

        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        if (raw.Length == 5
            && TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        return TimeOnly.TryParseExact(raw, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
