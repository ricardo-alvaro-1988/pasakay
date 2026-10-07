using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Hubs;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Auth;
using YaPasakay.Application.Common;
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

    public Task RiderOfferAsync(Guid riderId, string reference, CancellationToken cancellationToken = default) =>
        RiderOfferAsync(riderId, reference, null, cancellationToken);

    public async Task RiderOfferAsync(
        Guid riderId,
        string reference,
        DateTime? scheduledAtUtc,
        CancellationToken cancellationToken = default)
    {
        await RiderChangedAsync(riderId, "offer", cancellationToken);
        var body = "Open the app to accept.";
        if (!string.IsNullOrWhiteSpace(reference) && scheduledAtUtc is DateTime at)
        {
            var ph = PhilippineTime.ToPh(DateTime.SpecifyKind(at, DateTimeKind.Utc));
            body = $"Trip {reference} pickup {ph:MMM d, h:mm tt}.";
        }
        else if (!string.IsNullOrWhiteSpace(reference))
        {
            body = $"Trip {reference} is waiting.";
        }

        await PushRiderAsync(
            riderId,
            scheduledAtUtc is null ? "New job offer" : "Scheduled booking",
            body,
            "offer",
            cancellationToken);
    }

    public async Task OperatorScheduledBookingAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        if (trip.ScheduledAtUtc is not DateTime at)
        {
            return;
        }

        var ph = PhilippineTime.ToPh(DateTime.SpecifyKind(at, DateTimeKind.Utc));
        var pickup = string.IsNullOrWhiteSpace(trip.Pickup) ? "pickup" : trip.Pickup.Trim();
        db.OperatorNotifications.Add(new OperatorNotification
        {
            OperatorId = trip.OperatorId,
            Kind = NotificationKind.Scheduled,
            Title = "Scheduled booking",
            Body = $"{trip.Reference} · pickup {ph:MMM d, yyyy h:mm tt} · {pickup}. Assign a rider on Schedule."
        });
        await db.SaveChangesAsync(cancellationToken);
        await ops.Clients.Group(OpsHub.OperatorGroup(trip.OperatorId)).SendAsync(
            "opsAlert",
            new { reason = "scheduled", tripId = trip.Id, reference = trip.Reference },
            cancellationToken);
    }

    public async Task RiderNoticeAsync(IReadOnlyList<Guid> riderIds, CancellationToken cancellationToken = default)
    {
        foreach (var riderId in riderIds.Distinct())
        {
            await RiderChangedAsync(riderId, "notice", cancellationToken);
        }
    }

    public async Task RiderAssignedAsync(Guid riderId, string reference, bool hail, CancellationToken cancellationToken = default)
    {
        var reason = hail ? "hail-booked" : "assigned";
        await RiderChangedAsync(riderId, reason, cancellationToken);
        await PushRiderAsync(
            riderId,
            hail ? "Customer booked you" : "New booking assigned",
            string.IsNullOrWhiteSpace(reference)
                ? "A customer booked you. Open the app to start the trip."
                : $"Trip {reference} is waiting. Open the app to start.",
            reason,
            cancellationToken);
    }

    public async Task ScheduledPickupReminderAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        var pickup = TripAddress.Display(trip.PickupDetails, trip.Pickup);
        var body = string.IsNullOrWhiteSpace(pickup)
            ? $"Trip {trip.Reference} pickup is in 10 minutes."
            : $"Trip {trip.Reference} pickup in 10 minutes: {pickup}";
        if (trip.CustomerId is Guid customerId)
        {
            await CustomerTripAsync(customerId, "schedule-alarm", "Pickup in 10 minutes", body, cancellationToken);
        }

        if (trip.RiderId is Guid riderId)
        {
            await RiderChangedAsync(riderId, "schedule-alarm", cancellationToken);
            await PushRiderAsync(riderId, "Pickup in 10 minutes", body, "schedule-alarm", cancellationToken);
        }
    }

    private async Task PushRiderAsync(
        Guid riderId,
        string title,
        string body,
        string reason,
        CancellationToken cancellationToken)
    {
        var userId = await db.RiderProfiles.AsNoTracking()
            .Where(x => x.Id == riderId)
            .Select(x => x.AppUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId == Guid.Empty)
        {
            return;
        }

        await push.SendToUserAsync(
            userId,
            title,
            body,
            new Dictionary<string, string> { ["reason"] = reason },
            cancellationToken);
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

        if (trip.RiderId is Guid riderId)
        {
            await RiderChangedAsync(riderId, reason, cancellationToken);
        }
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
