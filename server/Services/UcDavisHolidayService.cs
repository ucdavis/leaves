using System.Net.Http.Json;

namespace Server.Services;

public sealed class UcDavisHolidayOptions
{
    public const string SectionName = "UcDavisHoliday";

    public string BaseUrl { get; init; } = string.Empty;
}

public interface IUcDavisHolidayService
{
    Task<IReadOnlyList<UniversityHoliday>> GetHolidaysAsync(CancellationToken cancellationToken);
}

public sealed class UcDavisHolidayService : IUcDavisHolidayService
{
    // The public Export page offers JSON separately from the legacy SOAP web service.
    // Its relative range keeps the data current without requiring client-side date parameters.
    private const string HolidayExportPath =
        "export.aspx?callback=jsonExport&type=Relative&start=Years%3A-3&end=Years%3A2&categories=1";

    private readonly HttpClient _httpClient;

    public UcDavisHolidayService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<UniversityHoliday>> GetHolidaysAsync(CancellationToken cancellationToken)
    {
        var dates = await _httpClient.GetFromJsonAsync<List<UcDavisImportantDate>>(
            HolidayExportPath,
            cancellationToken) ?? [];

        var holidaysByDate = new Dictionary<DateOnly, UniversityHoliday>();

        foreach (var date in dates)
        {
            if (string.IsNullOrWhiteSpace(date.Title))
            {
                continue;
            }

            var start = DateOnly.FromDateTime(date.StartDate);
            var end = DateOnly.FromDateTime(date.EndDate);
            if (end < start)
            {
                continue;
            }

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                holidaysByDate.TryAdd(day, new UniversityHoliday(
                    day.ToString("yyyy-MM-dd"),
                    date.Title.Trim()));
            }
        }

        return holidaysByDate.Values
            .OrderBy(holiday => holiday.Date)
            .ToArray();
    }

    private sealed record UcDavisImportantDate(
        string? Title,
        DateTime StartDate,
        DateTime EndDate);
}

public sealed record UniversityHoliday(string Date, string Name);
