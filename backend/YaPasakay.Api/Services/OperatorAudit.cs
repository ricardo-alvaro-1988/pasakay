using System.Security.Claims;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class OperatorAudit
{
    public static void Record(
        AppDbContext db,
        ClaimsPrincipal? user,
        Guid operatorId,
        AuditAction action,
        string summary)
    {
        Guid? actorId = null;
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(raw, out var id))
        {
            actorId = id;
        }

        var text = (summary ?? string.Empty).Trim();
        if (text.Length > 400)
        {
            text = text[..397] + "...";
        }

        db.AuditLogs.Add(new AuditLog
        {
            OperatorId = operatorId,
            ActorUserId = actorId,
            Action = action,
            Summary = text
        });
    }
}
