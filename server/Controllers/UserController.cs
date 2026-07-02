using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Server.Controllers;

public class UserController : ApiControllerBase
{
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var userName =
            GetClaimValue("name")
            ?? GetClaimValue(ClaimTypes.Name)
            ?? GetClaimValue("preferred_username")
            ?? GetClaimValue(ClaimTypes.Email)
            ?? userId;

        var userEmail =
            GetClaimValue("preferred_username")
            ?? GetClaimValue(ClaimTypes.Email)
            ?? GetClaimValue("email")
            ?? userName;

        var userRoles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var claimSummary = string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"));
        _logger.LogInformation(
            "UserController.Me resolved claims for user {UserId}: name={UserName}, email={UserEmail}, roles={Roles}. Raw claims: {Claims}",
            userId,
            userName,
            userEmail,
            string.Join(", ", userRoles),
            claimSummary);

        return Ok(new UserResponse(userId, userName, userEmail, userRoles));
    }

    private string? GetClaimValue(string claimType) => User.FindFirstValue(claimType);

    private sealed record UserResponse(string Id, string Name, string Email, IReadOnlyCollection<string> Roles);
}
