using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

public sealed class FacultyController : ApiControllerBase
{
    private readonly IFacultyDashboardService _facultyDashboardService;

    public FacultyController(IFacultyDashboardService facultyDashboardService)
    {
        _facultyDashboardService = facultyDashboardService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _facultyDashboardService.GetDashboardAsync(User, cancellationToken);
        if (dashboard == null)
        {
            return NotFound("The authenticated user does not have a faculty profile in Leaves.");
        }

        return Ok(dashboard);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateFacultyLeaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _facultyDashboardService.CreateLeaveRequestAsync(User, request, cancellationToken);
        if (result.MissingUser)
        {
            return NotFound("The authenticated user does not have a faculty profile in Leaves.");
        }

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors));
        }

        return Created($"/api/faculty/requests/{result.LeaveRequestId}", new
        {
            id = result.LeaveRequestId,
        });
    }
}
