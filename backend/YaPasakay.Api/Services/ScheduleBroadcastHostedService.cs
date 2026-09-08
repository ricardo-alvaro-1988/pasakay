namespace YaPasakay.Api.Services;

/// <summary>Wakes Pending scheduled trips when they enter the broadcast lead window.</summary>
public sealed class ScheduleBroadcastHostedService(IServiceScopeFactory scopes, ILogger<ScheduleBroadcastHostedService> log)
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
                await broadcast.BroadcastDueScheduledAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Scheduled trip broadcast sweep failed.");
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
