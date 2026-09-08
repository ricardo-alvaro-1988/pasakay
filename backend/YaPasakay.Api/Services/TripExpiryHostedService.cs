namespace YaPasakay.Api.Services;

/// <summary>Expires stale rider offers and auto-cancels unanswered Pending bookings.</summary>
public sealed class TripExpiryHostedService(IServiceScopeFactory scopes, ILogger<TripExpiryHostedService> log)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var broadcast = scope.ServiceProvider.GetRequiredService<TripBroadcastService>();
                await broadcast.ExpireStaleAsync(null, stoppingToken);
                var cancelled = await broadcast.ExpireUnassignedTripsAsync(stoppingToken);
                if (cancelled > 0)
                {
                    log.LogInformation("Auto-cancelled {Count} unassigned Pending trip(s).", cancelled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Trip expiry sweep failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
