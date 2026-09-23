using Microsoft.Extensions.Caching.Memory;

namespace Server.Services;

public interface IUniversityHolidayCache
{
    IReadOnlyList<UniversityHoliday> GetHolidays();

    Task RefreshAsync(CancellationToken cancellationToken);

    DateTime? LastSuccessfulRefreshUtc { get; }
}

public sealed class UniversityHolidayCache : IUniversityHolidayCache
{
    private const string CacheKey = "university-holidays";
    private readonly IMemoryCache _memoryCache;
    private readonly IUcDavisHolidayService _holidayService;

    public UniversityHolidayCache(
        IMemoryCache memoryCache,
        IUcDavisHolidayService holidayService)
    {
        _memoryCache = memoryCache;
        _holidayService = holidayService;
    }

    public DateTime? LastSuccessfulRefreshUtc =>
        _memoryCache.TryGetValue(CacheKey, out HolidayCalendarCacheEntry? entry)
            ? entry?.LastSuccessfulRefreshUtc
            : null;

    public IReadOnlyList<UniversityHoliday> GetHolidays()
    {
        if (_memoryCache.TryGetValue(CacheKey, out HolidayCalendarCacheEntry? entry) &&
            entry is not null)
        {
            return entry.Holidays;
        }

        throw new InvalidDataException("UC Davis holiday data has not been loaded.");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await FetchAndCacheAsync(cancellationToken);
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
        _memoryCache.Set(CacheKey, new HolidayCalendarCacheEntry(
            holidays,
            DateTime.UtcNow));

        return holidays;
    }

    private sealed record HolidayCalendarCacheEntry(
        IReadOnlyList<UniversityHoliday> Holidays,
        DateTime LastSuccessfulRefreshUtc);
}
