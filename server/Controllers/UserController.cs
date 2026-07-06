using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Server.Helpers;

namespace Server.Controllers;

public class UserController : ApiControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.GetUserId();
        var userName = User.Identity?.Name ?? userId;
        var userEmail =
            GetClaimValue("preferred_username")
            ?? GetClaimValue(ClaimTypes.Email)
            ?? userName;
        var entraObjectId = GetClaimValue("oid");

        var userRoles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
