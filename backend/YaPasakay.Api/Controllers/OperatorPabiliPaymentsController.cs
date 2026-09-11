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
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/pabili-payments")]
public class OperatorPabiliPaymentsController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OperatorPabiliPaymentMethodListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.OperatorPabiliPaymentMethods
            .AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Method)
            .ToListAsync(cancellationToken);
        return Ok(new OperatorPabiliPaymentMethodListResponse(rows.Select(Map).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<OperatorPabiliPaymentMethodItem>> Create(
        [FromBody] SaveOperatorPabiliPaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (await db.OperatorPabiliPaymentMethods.AnyAsync(
                x => x.OperatorId == op.Id && x.Method == request.Method,
                cancellationToken))
        {
            return Conflict(new { message = $"{request.Method} is already configured. Edit the existing row instead." });
        }

        var row = new OperatorPabiliPaymentMethod
        {
            OperatorId = op.Id,
            Method = request.Method,
            Label = NormalizeLabel(request.Method, request.Label),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
        };
        db.OperatorPabiliPaymentMethods.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperatorPabiliPaymentMethodItem>> Update(
        Guid id,
        [FromBody] SaveOperatorPabiliPaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var row = await db.OperatorPabiliPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Payment method not found." });
        }

        if (row.Method != request.Method
            && await db.OperatorPabiliPaymentMethods.AnyAsync(
                x => x.OperatorId == op.Id && x.Method == request.Method && x.Id != id,
                cancellationToken))
        {
            return Conflict(new { message = $"{request.Method} is already configured." });
        }

        row.Method = request.Method;
        row.Label = NormalizeLabel(request.Method, request.Label);
        row.IsActive = request.IsActive;
        row.SortOrder = request.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<OperatorPabiliPaymentMethodItem>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPabiliPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Payment method not found." });
        }

        row.IsActive = !row.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPabiliPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Payment method not found." });
        }

        db.OperatorPabiliPaymentMethods.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/qr")]
    public async Task<ActionResult<OperatorPabiliPaymentMethodItem>> UploadQr(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await db.OperatorPabiliPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Payment method not found." });
        }

        try
        {
            var path = await uploads.SaveAsync(file, $"pabili-payments/{row.Id}", "qr", cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "QR image file is required." });
            }

            row.QrImagePath = path;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(Map(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    static string? Validate(SaveOperatorPabiliPaymentMethodRequest request)
    {
        if (!Enum.IsDefined(request.Method))
        {
            return "Choose Cash, GCash, Maya, or Other.";
        }

        if (request.Method == PaymentMethod.Other
            && string.IsNullOrWhiteSpace(request.Label))
        {
            return "Label is required for Other.";
        }

        if (!string.IsNullOrWhiteSpace(request.Label) && request.Label.Trim().Length > 80)
        {
            return "Label is too long.";
        }

        return null;
    }

    static string NormalizeLabel(PaymentMethod method, string? label)
    {
        var trimmed = (label ?? string.Empty).Trim();
        if (trimmed.Length > 0)
        {
            return trimmed;
        }

        return method.ToString();
    }

    static OperatorPabiliPaymentMethodItem Map(OperatorPabiliPaymentMethod row) =>
        new(
            row.Id,
            row.Method,
            string.IsNullOrWhiteSpace(row.Label) ? row.Method.ToString() : row.Label,
            UploadUrls.FromPath(row.QrImagePath),
            row.IsActive,
            row.SortOrder,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
}
