using Microsoft.Extensions.Caching.Memory;

namespace Server.Services;

public interface IUniversityHolidayCache
{
    Task<IReadOnlyList<UniversityHoliday>> GetHolidaysAsync(CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);
}

public sealed class UniversityHolidayCache : IUniversityHolidayCache
{
    private const string CacheKey = "university-holidays";
    private readonly IMemoryCache _memoryCache;
    private readonly IUcDavisHolidayService _holidayService;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public UniversityHolidayCache(
        IMemoryCache memoryCache,
        IUcDavisHolidayService holidayService)
    {
        _memoryCache = memoryCache;
        _holidayService = holidayService;
    }

    public async Task<IReadOnlyList<UniversityHoliday>> GetHolidaysAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<UniversityHoliday>? holidays) &&
            holidays is not null)
        {
            return holidays;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(CacheKey, out holidays) && holidays is not null)
            {
                return holidays;
            }

            return await FetchAndCacheAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            await FetchAndCacheAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<UniversityHoliday>> FetchAndCacheAsync(CancellationToken cancellationToken)
    {
        var holidays = await _holidayService.GetHolidaysAsync(cancellationToken);
        if (holidays.Count == 0)
        {
            throw new InvalidDataException("UC Davis returned no holiday data.");
        }

        // Only replace the cache after a complete, validated fetch. Keeping the
        // value without an expiration lets callers continue using the last
        // known-good list when a scheduled refresh fails.
        _memoryCache.Set(CacheKey, holidays);

        return holidays;
    }
}
