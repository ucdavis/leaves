using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
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
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        var userName = await _userService.GetDisplayNameForUser(userId)
            ?? User.FindFirstValue("name")
            ?? User.Identity?.Name
            ?? userId;
        var userEmail =
            GetClaimValue("preferred_username")
            ?? GetClaimValue(ClaimTypes.Email)
            ?? userName;
        var entraObjectId =
            GetClaimValue(ClaimConstants.ObjectId)
            ?? GetClaimValue(ClaimConstants.Oid);

        var userRoles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = new UserResponse(userId, entraObjectId, userName, userEmail, userRoles);
        return Ok(response);
    }

    private string? GetClaimValue(string claimType) => User.FindFirstValue(claimType);

    private sealed record UserResponse(
        string Id,
        string? EntraObjectId,
        string Name,
        string Email,
        IReadOnlyCollection<string> Roles);
}
