using System.Security.Claims;
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
        if (!CanAccessFacultyWorkspace(User))
        {
            return Forbid();
        }

        var dashboard = await _facultyDashboardService.GetDashboardAsync(User, cancellationToken);
        if (dashboard == null)
        {
            return NotFound("The authenticated user does not have a faculty profile in Leaves.");
        }

        return Ok(dashboard);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        if (!CanAccessFacultyWorkspace(User))
        {
            return Forbid();
        }

        var history = await _facultyDashboardService.GetHistoryAsync(User, cancellationToken);
        if (history == null)
        {
            return NotFound("The authenticated user does not have a faculty profile in Leaves.");
        }

        return Ok(history);
    }

    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> GetRequest(int id, CancellationToken cancellationToken)
    {
        if (!CanAccessFacultyWorkspace(User))
        {
            return Forbid();
        }

        var request = await _facultyDashboardService.GetRequestAsync(User, id, cancellationToken);
        if (request == null)
        {
            return NotFound("The requested leave request was not found.");
        }

        return Ok(request);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateFacultyLeaveRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanAccessFacultyWorkspace(User))
        {
            return Forbid();
        }

        var result = await _facultyDashboardService.CreateLeaveRequestAsync(User, request, cancellationToken);
        if (result.MissingUser)
        {
            return NotFound("The authenticated user does not have a faculty profile in Leaves.");
        }

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors));
        }

        return CreatedAtAction(nameof(GetRequest), new { id = result.LeaveRequestId }, new
        {
            id = result.LeaveRequestId,
        });
    }

    private static bool CanAccessFacultyWorkspace(ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Any(roleClaim =>
            roleClaim.Value.Equals("faculty", StringComparison.OrdinalIgnoreCase) ||
            roleClaim.Value.Equals("chair", StringComparison.OrdinalIgnoreCase));
    }
}
