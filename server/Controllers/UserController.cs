using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Server.Helpers;
using Server.Services;

namespace Server.Controllers;

public class UserController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var userName = await _userService.GetDisplayNameForUser(userId, cancellationToken)
            ?? User.FindFirstValue("name")
            ?? User.Identity?.Name
            ?? userId;
        var userEmail =
            GetClaimValue("preferred_username")
            ?? GetClaimValue(ClaimTypes.Email)
            ?? userName;
        var entraObjectId = GetClaimValue("oid");

        var userRoles = User.HasClaim("dev_persona", "true")
            ? User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : (await _userService.GetRolesForUser(userId, cancellationToken)).ToArray();

        return Ok(new UserResponse(userId, entraObjectId, userName, userEmail, userRoles));
    }

    private string? GetClaimValue(string claimType) => User.FindFirstValue(claimType);

    private sealed record UserResponse(
        string Id,
        string? EntraObjectId,
        string Name,
        string Email,
        IReadOnlyCollection<string> Roles);
}
