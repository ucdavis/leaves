using Microsoft.Extensions.Hosting;

namespace Server.Services;

public sealed class UniversityHolidayCacheRefreshService : BackgroundService
{
    private static readonly TimeSpan SuccessfulRefreshInterval = TimeSpan.FromDays(30);
    private static readonly TimeSpan FailedRefreshRetryInterval = TimeSpan.FromDays(1);

    private readonly IUniversityHolidayCache _holidayCache;
    private readonly ILogger<UniversityHolidayCacheRefreshService> _logger;

    public UniversityHolidayCacheRefreshService(
        IUniversityHolidayCache holidayCache,
        ILogger<UniversityHolidayCacheRefreshService> logger)
    {
        _holidayCache = holidayCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastRefreshSucceeded = await RefreshSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = lastRefreshSucceeded
                ? SuccessfulRefreshInterval
                : FailedRefreshRetryInterval;
            using var timer = new PeriodicTimer(interval);
            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }

            lastRefreshSucceeded = await RefreshSafelyAsync(stoppingToken);
        }
    }

    private async Task<bool> RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _holidayCache.RefreshAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to refresh the UC Davis holiday cache.");
            return false;
        }
    }
}
