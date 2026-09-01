using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

public sealed class ApprovalWorkspaceController : ApiControllerBase
{
    private readonly IApprovalWorkspaceService _workspaceService;

    public ApprovalWorkspaceController(IApprovalWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApprovalWorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspace(CancellationToken cancellationToken)
    {
        if (!CanAccessApprovalWorkspace(User))
        {
            return Forbid();
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(User, cancellationToken);
        if (workspace == null)
        {
            return NotFound("The authenticated user could not be matched to an approval workspace.");
        }

        return Ok(workspace);
    }

    [HttpPost("requests/{id:int}/decision")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitDecision(
        int id,
        [FromBody] SubmitApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanAccessApprovalWorkspace(User))
        {
            return Forbid();
        }

        var result = await _workspaceService.SubmitDecisionAsync(
            User,
            id,
            request,
            cancellationToken);

        if (result.MissingRequest)
        {
            return NotFound("The pending leave request was not found in your approval workspace.");
        }

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors));
        }

        return NoContent();
    }

    private static bool CanAccessApprovalWorkspace(ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Any(roleClaim =>
            roleClaim.Value.Equals("chair", StringComparison.OrdinalIgnoreCase) ||
            roleClaim.Value.Equals("cao", StringComparison.OrdinalIgnoreCase));
    }
}
