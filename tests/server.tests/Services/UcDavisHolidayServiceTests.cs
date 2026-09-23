using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Server.Services;

namespace Server.Tests.Services;

public sealed class UcDavisHolidayServiceTests
{
    [Fact]
    public async Task GetHolidaysAsync_expands_multi_day_holidays_and_orders_each_calendar_day()
    {
        const string response = """
            [
              {
                "title": "Winter Holiday",
                "startDate": "2026-12-24T00:00:00",
                "endDate": "2026-12-25T00:00:00"
              },
              {
                "title": "Veterans Day",
                "startDate": "2026-11-11T00:00:00",
                "endDate": "2026-11-11T00:00:00"
              }
            ]
            """;
        using var client = new HttpClient(new StaticResponseHandler(response))
        {
            BaseAddress = new Uri("https://dates.ucdavis.edu/"),
        };
        var service = new UcDavisHolidayService(client);

        var holidays = await service.GetHolidaysAsync(CancellationToken.None);

        holidays.Should().BeEquivalentTo([
            new UniversityHoliday("2026-11-11", "Veterans Day"),
            new UniversityHoliday("2026-12-24", "Winter Holiday"),
            new UniversityHoliday("2026-12-25", "Winter Holiday"),
        ], options => options.WithStrictOrdering());
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _response;

        public StaticResponseHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.RequestUri!.PathAndQuery.Should().Contain("callback=jsonExport");
            request.RequestUri.Query.Should().Contain("categories=1");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json"),
            });
        }
    }
}
