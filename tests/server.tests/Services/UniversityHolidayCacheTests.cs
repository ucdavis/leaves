using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Server.Services;

namespace Server.Tests.Services;

public sealed class UniversityHolidayCacheTests
{
    [Fact]
    public async Task GetHolidaysAsync_returns_cached_holidays_without_another_upstream_request()
    {
        var upstream = new StubHolidayService([
            new UniversityHoliday("2026-11-11", "Veterans Day"),
        ]);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new UniversityHolidayCache(memoryCache, upstream);

        var first = await cache.GetHolidaysAsync(CancellationToken.None);
        var second = await cache.GetHolidaysAsync(CancellationToken.None);

        second.Should().BeSameAs(first);
        upstream.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RefreshAsync_keeps_the_last_known_good_value_when_the_upstream_request_fails()
    {
        var upstream = new StubHolidayService([
            new UniversityHoliday("2026-11-11", "Veterans Day"),
        ]);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new UniversityHolidayCache(memoryCache, upstream);
        var cachedHolidays = await cache.GetHolidaysAsync(CancellationToken.None);
        var lastSuccessfulRefreshUtc = cache.LastSuccessfulRefreshUtc;
        upstream.Exception = new HttpRequestException("UC Davis is unavailable.");

        var refresh = () => cache.RefreshAsync(CancellationToken.None);

        await refresh.Should().ThrowAsync<HttpRequestException>();
        var holidaysAfterFailedRefresh = await cache.GetHolidaysAsync(CancellationToken.None);
        holidaysAfterFailedRefresh.Should().BeSameAs(cachedHolidays);
        cache.LastSuccessfulRefreshUtc.Should().Be(lastSuccessfulRefreshUtc);
    }

    private sealed class StubHolidayService : IUcDavisHolidayService
    {
        private readonly IReadOnlyList<UniversityHoliday> _holidays;

        public StubHolidayService(IReadOnlyList<UniversityHoliday> holidays)
        {
            _holidays = holidays;
        }

        public int CallCount { get; private set; }

        public Exception? Exception { get; set; }

        public Task<IReadOnlyList<UniversityHoliday>> GetHolidaysAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(_holidays);
        }
    }
}
