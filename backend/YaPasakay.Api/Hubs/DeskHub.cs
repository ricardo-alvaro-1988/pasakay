using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Hubs;

[Authorize(Roles = "Customer,Rider")]
public class DeskHub(AppDbContext db) : Hub
{
    public static string RiderGroup(Guid riderId) => $"rider:{riderId:D}";
    public static string CustomerGroup(Guid customerId) => $"customer:{customerId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = AdminAccess.UserId(Context.User);
        if (userId is null)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (Context.User?.IsInRole("Rider") == true)
        {
            var riderId = await db.RiderProfiles.AsNoTracking()
                .Where(x => x.AppUserId == userId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
            if (riderId is Guid id)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RiderGroup(id));
            }
        }
        else if (Context.User?.IsInRole("Customer") == true)
        {
            var customerId = await db.CustomerProfiles.AsNoTracking()
                .Where(x => x.AppUserId == userId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
            if (customerId is Guid id)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CustomerGroup(id));
            }
        }

        await base.OnConnectedAsync();
    }
}
