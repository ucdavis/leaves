using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

public sealed class UniversityHolidaysController : ApiControllerBase
{
    private readonly IUniversityHolidayCache _holidayCache;

    public UniversityHolidaysController(IUniversityHolidayCache holidayCache)
    {
        _holidayCache = holidayCache;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UniversityHoliday>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHolidays(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _holidayCache.GetHolidaysAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            return HolidayDataUnavailable();
        }
        catch (JsonException)
        {
            return HolidayDataUnavailable();
        }
        catch (InvalidDataException)
        {
            return HolidayDataUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HolidayDataUnavailable();
        }
    }

    private IActionResult HolidayDataUnavailable()
    {
        return Problem(
            detail: "UC Davis holiday data is temporarily unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
