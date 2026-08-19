using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Hubs;

[Authorize(Roles = "Customer,Rider")]
public class TripChatHub(AppDbContext db) : Hub
{
    public static string GroupName(Guid tripId) => $"trip:{tripId:D}";

    public async Task JoinTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id))
        {
            throw new HubException("Invalid trip.");
        }

        if (!await CanAccessAsync(id))
        {
            throw new HubException("Chat is not available for this trip.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(id));
    }

    public async Task LeaveTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(id));
    }

    private async Task<bool> CanAccessAsync(Guid tripId)
    {
        var userId = AdminAccess.UserId(Context.User);
        if (userId is null)
        {
            return false;
        }

        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tripId);
        if (trip is null || !TripChatService.CanChat(trip))
        {
            return false;
        }

        if (Context.User?.IsInRole("Customer") == true)
        {
            return await db.CustomerProfiles.AnyAsync(x => x.AppUserId == userId && x.Id == trip.CustomerId);
        }

        if (Context.User?.IsInRole("Rider") == true)
        {
            return await db.RiderProfiles.AnyAsync(x => x.AppUserId == userId && x.Id == trip.RiderId);
        }

        return false;
    }
}
