using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Hubs;

[Authorize(Roles = "Admin,Operator")]
public class OpsHub(AppDbContext db) : Hub
{
    public static string OperatorGroup(Guid operatorId) => $"operator:{operatorId:D}";
    public static string AdminGroup() => "admin:all";

    public override async Task OnConnectedAsync()
    {
        var userId = AdminAccess.UserId(Context.User);
        if (userId is null)
        {
            await base.OnConnectedAsync();
            return;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.IsActive)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (user.Role == UserRole.Operator && user.OperatorId is Guid operatorId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OperatorGroup(operatorId));
        }
        else if (user.Role == UserRole.Admin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup());
        }

        await base.OnConnectedAsync();
    }
}
