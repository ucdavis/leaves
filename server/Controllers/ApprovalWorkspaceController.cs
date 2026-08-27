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

    private static bool CanAccessApprovalWorkspace(ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Any(roleClaim =>
            roleClaim.Value.Equals("chair", StringComparison.OrdinalIgnoreCase) ||
            roleClaim.Value.Equals("cao", StringComparison.OrdinalIgnoreCase));
    }
}
