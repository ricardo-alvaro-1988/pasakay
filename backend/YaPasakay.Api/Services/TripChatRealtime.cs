using Microsoft.AspNetCore.SignalR;
using YaPasakay.Api.Hubs;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;

namespace YaPasakay.Api.Services;

public class TripChatRealtime(IHubContext<TripChatHub> chat, IHubContext<DeskHub> desk)
{
    public async Task BroadcastAsync(Trip trip, RideChatMessageItem message, CancellationToken cancellationToken = default)
    {
        await chat.Clients.Group(TripChatHub.GroupName(trip.Id))
            .SendAsync("chatMessage", message, cancellationToken);
        await desk.Clients.Group(DeskHub.RiderGroup(trip.RiderId))
            .SendAsync("chatMessage", message, cancellationToken);
        if (trip.CustomerId is Guid customerId)
        {
            await desk.Clients.Group(DeskHub.CustomerGroup(customerId))
                .SendAsync("chatMessage", message, cancellationToken);
        }
    }
}
