namespace YaPasakay.Application.Auth;

public interface IPushNotifier
{
    Task SendToUserAsync(Guid appUserId, string title, string body, IReadOnlyDictionary<string, string>? data = null, CancellationToken cancellationToken = default);
}
