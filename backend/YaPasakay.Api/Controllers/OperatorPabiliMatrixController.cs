using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[Route("api/operator/pabili-matrix")]
public class OperatorPabiliMatrixController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PabiliMatrixResponse>> Get(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var matrix = await EnsureAsync(op, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(op, matrix));
    }

    [HttpPut]
    public async Task<ActionResult<PabiliMatrixResponse>> Save(
        [FromBody] SavePabiliMatrixRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        if (request.BaseFareAmount < 0 || request.KmScope < 0 || request.SucceedingKm < 0)
        {
            return BadRequest(new { message = "Base fare, KM scope, and succeeding KM cannot be negative." });
        }

        var fareError = FareCommissionSplit.Validate(
            op.PabiliFareSystemCommissionPercent,
            request.FareOperatorCommissionPercent,
            request.FareRiderCommissionPercent);
        if (fareError is not null)
        {
            return BadRequest(new { message = $"Fare commission: {fareError}" });
        }

        var markupError = FareCommissionSplit.Validate(
            op.PabiliMarkupSystemCommissionPercent,
            request.MarkupOperatorCommissionPercent,
            request.MarkupRiderCommissionPercent);
        if (markupError is not null)
        {
            return BadRequest(new { message = $"Markup commission: {markupError}" });
        }

        var matrix = await EnsureAsync(op, cancellationToken);
        matrix.BaseFareAmount = FareCommissionSplit.Round(request.BaseFareAmount);
        matrix.KmScope = FareCommissionSplit.Round(request.KmScope);
        matrix.SucceedingKm = FareCommissionSplit.Round(request.SucceedingKm);
        matrix.FareOperatorCommissionPercent = FareCommissionSplit.Round(request.FareOperatorCommissionPercent);
        matrix.FareRiderCommissionPercent = FareCommissionSplit.Round(request.FareRiderCommissionPercent);
        matrix.MarkupOperatorCommissionPercent = FareCommissionSplit.Round(request.MarkupOperatorCommissionPercent);
        matrix.MarkupRiderCommissionPercent = FareCommissionSplit.Round(request.MarkupRiderCommissionPercent);
        matrix.IsActive = request.IsActive;
        matrix.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(op, matrix));
    }

    private async Task<PabiliMatrix> EnsureAsync(Operator op, CancellationToken cancellationToken)
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

    private static PabiliMatrixResponse Map(Operator op, PabiliMatrix matrix) =>
        new(
            op.Id,
            op.CompanyName,
            FareCommissionSplit.Round(matrix.BaseFareAmount),
            FareCommissionSplit.Round(matrix.KmScope),
            FareCommissionSplit.Round(matrix.SucceedingKm),
            FareCommissionSplit.Round(op.PabiliFareSystemCommissionPercent),
            FareCommissionSplit.Round(matrix.FareOperatorCommissionPercent),
            FareCommissionSplit.Round(matrix.FareRiderCommissionPercent),
            FareCommissionSplit.Round(op.PabiliMarkupSystemCommissionPercent),
            FareCommissionSplit.Round(matrix.MarkupOperatorCommissionPercent),
            FareCommissionSplit.Round(matrix.MarkupRiderCommissionPercent),
            matrix.IsActive);
}
