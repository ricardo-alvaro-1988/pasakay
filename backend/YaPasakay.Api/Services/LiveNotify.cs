using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Hubs;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public class LiveNotify(IHubContext<DeskHub> desk, IHubContext<OpsHub> ops, IPushNotifier push, AppDbContext db)
{
    public Task RiderChangedAsync(Guid riderId, string reason, CancellationToken cancellationToken = default) =>
        desk.Clients.Group(DeskHub.RiderGroup(riderId))
            .SendAsync("deskChanged", new { reason }, cancellationToken);

    public Task CustomerChangedAsync(Guid customerId, string reason, CancellationToken cancellationToken = default) =>
        desk.Clients.Group(DeskHub.CustomerGroup(customerId))
            .SendAsync("deskChanged", new { reason }, cancellationToken);

    public async Task RiderOfferAsync(Guid riderId, string reference, CancellationToken cancellationToken = default)
    {
        await RiderChangedAsync(riderId, "offer", cancellationToken);
        var userId = await db.RiderProfiles.AsNoTracking()
            .Where(x => x.Id == riderId)
            .Select(x => x.AppUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId != Guid.Empty)
        {
            await push.SendToUserAsync(
                userId,
                "New job offer",
                string.IsNullOrWhiteSpace(reference) ? "Open Ya! Pasakay to accept." : $"Trip {reference} is waiting.",
                new Dictionary<string, string> { ["reason"] = "offer" },
                cancellationToken);
        }
    }

    public async Task CustomerTripAsync(Guid customerId, string reason, string title, string body, CancellationToken cancellationToken = default)
    {
        await CustomerChangedAsync(customerId, reason, cancellationToken);
        var userId = await db.CustomerProfiles.AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => x.AppUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId != Guid.Empty)
        {
            await push.SendToUserAsync(
                userId,
                title,
                body,
                new Dictionary<string, string> { ["reason"] = reason },
                cancellationToken);
        }
    }

    public async Task TripPartiesAsync(Trip trip, string reason, CancellationToken cancellationToken = default)
    {
        if (trip.CustomerId is Guid customerId)
        {
            await CustomerChangedAsync(customerId, reason, cancellationToken);
        }

        await RiderChangedAsync(trip.RiderId, reason, cancellationToken);
    }

    public async Task SosPushAsync(Trip trip, SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            reason = "sos",
            tripId = trip.Id,
            ticketId = ticket.Id,
            reference = trip.Reference,
            operatorId = trip.OperatorId,
            lat = ticket.SosLat,
            lng = ticket.SosLng,
            openedBy = ticket.OpenedBy.ToString(),
            atUtc = ticket.SosAtUtc,
        };

        await Task.WhenAll(
            ops.Clients.Group(OpsHub.OperatorGroup(trip.OperatorId)).SendAsync("opsAlert", payload, cancellationToken),
            ops.Clients.Group(OpsHub.AdminGroup()).SendAsync("opsAlert", payload, cancellationToken));

        await TripPartiesAsync(trip, "sos", cancellationToken);

        var pushTargets = new List<Guid>();
        if (trip.Rider?.AppUserId is Guid riderUser)
        {
            pushTargets.Add(riderUser);
        }

        if (trip.Customer?.AppUserId is Guid customerUser)
        {
            pushTargets.Add(customerUser);
        }

        if (trip.OperatorId != Guid.Empty)
        {
            var opUsers = await db.Users.AsNoTracking()
                .Where(x => x.OperatorId == trip.OperatorId && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            pushTargets.AddRange(opUsers);
        }

        foreach (var id in pushTargets.Distinct())
        {
            await push.SendToUserAsync(id, "SOS alert", $"SOS on {trip.Reference}", new Dictionary<string, string> { ["reason"] = "sos" }, cancellationToken);
        }
    }

    public async Task ChatMessageAsync(Trip trip, RideChatMessageItem message, CancellationToken cancellationToken = default)
    {
        Guid? targetUserId = null;
        if (message.Sender == ChatSender.Customer)
        {
            targetUserId = await db.RiderProfiles.AsNoTracking()
                .Where(x => x.Id == trip.RiderId)
                .Select(x => (Guid?)x.AppUserId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (trip.CustomerId is Guid customerId)
        {
            targetUserId = await db.CustomerProfiles.AsNoTracking()
                .Where(x => x.Id == customerId)
                .Select(x => (Guid?)x.AppUserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (targetUserId is not Guid userId || userId == Guid.Empty)
        {
            return;
        }

        var preview = string.IsNullOrWhiteSpace(message.PhotoUrl)
            ? (string.IsNullOrWhiteSpace(message.Body) ? "New message" : message.Body.Trim())
            : string.IsNullOrWhiteSpace(message.Body) ? "Sent a photo" : message.Body.Trim();
        if (preview.Length > 120)
        {
            preview = preview[..117] + "...";
        }

        await push.SendToUserAsync(
            userId,
            trip.Reference.Length > 0 ? $"Chat · {trip.Reference}" : "New chat",
            preview,
            new Dictionary<string, string>
            {
                ["reason"] = "chat",
                ["tripId"] = trip.Id.ToString(),
            },
            cancellationToken);
    }
}
