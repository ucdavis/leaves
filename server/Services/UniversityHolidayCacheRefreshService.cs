using Microsoft.Extensions.Hosting;

namespace Server.Services;

public sealed class UniversityHolidayCacheRefreshService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);

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
        await RefreshSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _holidayCache.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to refresh the UC Davis holiday cache.");
        }
    }
}
