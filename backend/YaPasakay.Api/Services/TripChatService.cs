using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class TripChatService
{
    public const int MaxBodyLength = 400;
    public const long MaxPhotoBytes = 6_000_000;

    public static bool CanView(Trip trip) =>
        trip.Status is TripStatus.Waiting or TripStatus.Ongoing or TripStatus.Completed or TripStatus.Cancelled;

    public static bool CanChat(Trip trip) =>
        trip.Status is TripStatus.Waiting or TripStatus.Ongoing;

    public static async Task<IReadOnlyList<RideChatMessageItem>> ListAsync(
        AppDbContext db,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var rows = await db.TripChatMessages
            .Where(x => x.TripId == tripId)
            .OrderBy(x => x.SentAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public static async Task<(RideChatMessageItem? Message, string? Error)> SendAsync(
        AppDbContext db,
        Trip trip,
        ChatSender sender,
        string? body,
        string? photoPath,
        CancellationToken cancellationToken)
    {
        if (!CanChat(trip))
        {
            return (null, "Chat is only available while the trip is waiting or ongoing.");
        }

        var text = (body ?? string.Empty).Trim();
        var hasPhoto = !string.IsNullOrWhiteSpace(photoPath);
        if (text.Length == 0 && !hasPhoto)
        {
            return (null, "Type a message or send a photo.");
        }

        if (text.Length > MaxBodyLength)
        {
            return (null, $"Keep messages under {MaxBodyLength} characters.");
        }

        var now = DateTime.UtcNow;
        var message = new TripChatMessage
        {
            TripId = trip.Id,
            Sender = sender,
            Body = text,
            PhotoPath = hasPhoto ? photoPath : null,
            SentAtUtc = now
        };
        db.TripChatMessages.Add(message);
        trip.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return (Map(message), null);
    }

    public static async Task<(string? Path, string? Error)> SavePhotoAsync(
        UploadStore uploads,
        IFormFile? photo,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            return (null, "Choose a photo first.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            return (null, "Keep photos under 6 MB.");
        }

        try
        {
            var path = await uploads.SaveAsync(photo, "chat", $"{tripId:N}-{Guid.NewGuid():N}", cancellationToken);
            return string.IsNullOrWhiteSpace(path) ? (null, "Could not save that photo.") : (path, null);
        }
        catch (InvalidOperationException ex)
        {
            return (null, ex.Message);
        }
    }

    public static RideChatMessageItem Map(TripChatMessage message) =>
        new(
            message.Id,
            message.Sender,
            message.Body,
            DateTime.SpecifyKind(message.SentAtUtc, DateTimeKind.Utc),
            UploadUrls.FromPath(message.PhotoPath));
}
