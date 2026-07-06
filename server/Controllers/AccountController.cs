using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string DevPersonaClaimType = "dev_persona";

    private readonly IWebHostEnvironment _environment;

    public AccountController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("login")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Login(string? returnUrl, [FromQuery(Name = "as")] string? asOption)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl, "/");

        if (!IsDevLoopback(HttpContext))
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect(safeReturnUrl);
            }

            return Challenge(
                new AuthenticationProperties { RedirectUri = safeReturnUrl },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        var normalizedAs = asOption?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedAs))
        {
            return Redirect(BuildDevLoginUrl(safeReturnUrl, error: null));
        }

        return normalizedAs switch
        {
            "self" => Challenge(
                new AuthenticationProperties { RedirectUri = safeReturnUrl },
                OpenIdConnectDefaults.AuthenticationScheme),
            "admin" => SignInAsDevPersona(
                displayName: "Local Admin",
                email: "admin@local.test",
                userId: "dev-admin",
                safeReturnUrl: safeReturnUrl,
                roles: ["Admin"]),
            "requester" => SignInAsDevPersona(
                displayName: "Local Requester",
                email: "requester@local.test",
                userId: "dev-requester",
                safeReturnUrl: safeReturnUrl,
                roles: ["User"]),
            "unauthorized" => SignInAsDevPersona(
                displayName: "Local Unauthorized",
                email: "unauthorized@local.test",
                userId: "dev-unauthorized",
                safeReturnUrl: safeReturnUrl,
                roles: []),
            _ => Redirect(BuildDevLoginUrl(safeReturnUrl, $"Unknown login option '{asOption}'.")),
        };
    }

    private static string NormalizeReturnUrl(string? returnUrl, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return fallbackPath;
        }

        var trimmed = returnUrl.Trim();
        if (!trimmed.StartsWith('/') || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return fallbackPath;
        }

        return trimmed;
    }

    private bool IsDevLoopback(HttpContext context)
    {
        if (!_environment.IsDevelopment())
        {
            return false;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        var effectiveIp = IPAddress.IsLoopback(remoteIp) ? ParseFirstForwardedFor(context) ?? remoteIp : remoteIp;
        return IPAddress.IsLoopback(effectiveIp);
    }

    private static IPAddress? ParseFirstForwardedFor(HttpContext context)
    {
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(xForwardedFor))
        {
            return null;
        }

        var firstIp = xForwardedFor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(firstIp) && IPAddress.TryParse(firstIp, out var parsed)
            ? parsed
            : null;
    }

    private static string BuildDevLoginUrl(string safeReturnUrl, string? error)
    {
        var query = new List<string>
        {
            "devLogin=1",
            $"returnUrl={Uri.EscapeDataString(safeReturnUrl)}",
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            query.Add($"error={Uri.EscapeDataString(error)}");
        }

        return $"/about?{string.Join("&", query)}";
    }

    private IActionResult SignInAsDevPersona(
        string displayName,
        string email,
        string userId,
        string safeReturnUrl,
        IReadOnlyCollection<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new("name", displayName),
            new(ClaimTypes.Email, email),
            new("preferred_username", email),
            new(DevPersonaClaimType, "true"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        return SignIn(
            principal,
            new AuthenticationProperties { RedirectUri = safeReturnUrl },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
