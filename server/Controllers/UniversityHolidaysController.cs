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
    public IActionResult GetHolidays()
    {
        try
        {
            return Ok(_holidayCache.GetHolidays());
        }
        catch (InvalidDataException)
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
