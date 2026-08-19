using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Infrastructure.Services;

public static class SosAlerts
{
    public static async Task<(SupportTicket? Ticket, string? Error)> RaiseAsync(
        AppDbContext db,
        Trip trip,
        SupportOpenedBy openedBy,
        Guid? riderId,
        Guid? customerId,
        string? message,
        double? lat,
        double? lng,
        CancellationToken cancellationToken)
    {
        if (trip.Status is not TripStatus.Ongoing and not TripStatus.Waiting)
        {
            return (null, "SOS is only available during an active trip.");
        }

        var rider = trip.Rider
            ?? await db.RiderProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == trip.RiderId, cancellationToken);

        var existing = await db.SupportTickets
            .FirstOrDefaultAsync(
                x => x.TripId == trip.Id && x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open,
                cancellationToken);
        if (existing is not null)
        {
            var (updateLat, updateLng) = ResolveSosLocation(lat, lng, trip, openedBy, rider);
            if (updateLat is not null)
            {
                existing.SosLat = updateLat;
                existing.SosLng = updateLng;
            }

            existing.SosAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return (existing, null);
        }

        var opener = openedBy == SupportOpenedBy.Rider ? "Rider" : "Customer";
        var body = string.IsNullOrWhiteSpace(message)
            ? $"{opener} pressed SOS during booking {trip.Reference}. Operator must respond in their municipality."
            : message.Trim();

        var (sosLat, sosLng) = ResolveSosLocation(lat, lng, trip, openedBy, rider);

        var ticket = new SupportTicket
        {
            OperatorId = trip.OperatorId,
            TripId = trip.Id,
            RiderId = riderId ?? trip.RiderId,
            CustomerId = customerId ?? trip.CustomerId,
            Kind = SupportKind.Sos,
            Status = SupportStatus.Open,
            OpenedBy = openedBy,
            Subject = "SOS",
            Body = body,
            SosLat = sosLat,
            SosLng = sosLng,
            SosAtUtc = DateTime.UtcNow
        };
        db.SupportTickets.Add(ticket);

        var opName = await db.Operators
            .Where(x => x.Id == trip.OperatorId)
            .Select(x => x.CompanyName)
            .FirstAsync(cancellationToken);

        db.AdminNotifications.Add(new AdminNotification
        {
            Kind = NotificationKind.Sos,
            SupportTicket = ticket,
            Title = "SOS alert",
            Body = $"{opener} pressed SOS on {trip.Reference} under {opName}."
        });

        await db.SaveChangesAsync(cancellationToken);
        return (ticket, null);
    }

    public static async Task EnsureAdminAlertAsync(AppDbContext db, SupportTicket ticket, CancellationToken cancellationToken)
    {
        if (ticket.Kind != SupportKind.Sos)
        {
            return;
        }

        if (await db.AdminNotifications.AnyAsync(x => x.SupportTicketId == ticket.Id, cancellationToken))
        {
            return;
        }

        var tripRef = ticket.TripId is null
            ? "a trip"
            : await db.Trips.Where(x => x.Id == ticket.TripId).Select(x => x.Reference).FirstOrDefaultAsync(cancellationToken)
              ?? "a trip";
        var opName = await db.Operators
            .Where(x => x.Id == ticket.OperatorId)
            .Select(x => x.CompanyName)
            .FirstAsync(cancellationToken);
        var opener = ticket.OpenedBy == SupportOpenedBy.Rider ? "Rider" : "Customer";

        db.AdminNotifications.Add(new AdminNotification
        {
            Kind = NotificationKind.Sos,
            SupportTicketId = ticket.Id,
            Title = "SOS alert",
            Body = $"{opener} pressed SOS on {tripRef} under {opName}."
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (double? Lat, double? Lng) ResolveSosLocation(
        double? lat,
        double? lng,
        Trip trip,
        SupportOpenedBy openedBy,
        RiderProfile? rider)
    {
        if (IsValid(lat, lng))
        {
            return (lat, lng);
        }

        if (openedBy == SupportOpenedBy.Rider
            && IsValid(rider?.LastLat, rider?.LastLng))
        {
            return (rider!.LastLat, rider.LastLng);
        }

        if (IsValid(trip.PickupLat, trip.PickupLng))
        {
            return (trip.PickupLat, trip.PickupLng);
        }

        if (IsValid(trip.DropoffLat, trip.DropoffLng))
        {
            return (trip.DropoffLat, trip.DropoffLng);
        }

        return (null, null);
    }

    private static bool IsValid(double? lat, double? lng) =>
        lat is >= -90 and <= 90
        && lng is >= -180 and <= 180
        && !(lat == 0 && lng == 0);
}
