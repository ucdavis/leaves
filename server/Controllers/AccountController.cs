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
            return RenderDevLoginPage(safeReturnUrl, error: null);
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
            _ => RenderDevLoginPage(safeReturnUrl, $"Unknown login option '{asOption}'."),
        };
    }

    private string NormalizeReturnUrl(string? returnUrl, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return fallbackPath;
        }

        var trimmed = returnUrl.Trim();
        if (!Url.IsLocalUrl(trimmed))
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

    private ContentResult RenderDevLoginPage(string safeReturnUrl, string? error)
    {
        var encodedReturnUrl = Uri.EscapeDataString(safeReturnUrl);
        var safeReturnUrlText = WebUtility.HtmlEncode(safeReturnUrl);
        var errorMarkup = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""
              <div style="margin-top: 24px; border-radius: 16px; border: 1px solid #fecaca; background: #fef2f2; color: #991b1b; padding: 16px 18px; font-size: 14px;">
                {WebUtility.HtmlEncode(error)}
              </div>
              """;

        var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Leaves Dev Login</title>
        </head>
        <body style="margin: 0; background: #f5f7fb; color: #1f2937; font-family: Arial, sans-serif;">
          <main style="min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 32px 16px;">
            <section style="width: 100%; max-width: 720px; border-radius: 24px; border: 1px solid #dbe4f0; background: #ffffff; padding: 32px; box-shadow: 0 18px 50px rgba(15, 23, 42, 0.08);">
              <div style="font-size: 12px; font-weight: 700; letter-spacing: 0.18em; text-transform: uppercase; color: #0f4c81;">Local Development</div>
              <h1 style="margin: 12px 0 0; font-size: 36px; line-height: 1.1;">Choose a login</h1>
              <p style="margin: 16px 0 0; max-width: 560px; color: #4b5563; font-size: 16px; line-height: 1.6;">
                Pick a persona to test role-based behavior in Leaves, or continue with your real Entra sign-in.
              </p>
              {{errorMarkup}}
              <div style="display: grid; gap: 14px; margin-top: 28px;">
                {{BuildDevLoginOption("Login as Admin", "Grants the local Admin role for testing admin-only UI.", $"/login?as=admin&returnUrl={encodedReturnUrl}")}}
                {{BuildDevLoginOption("Login as Requester", "Simulates a standard signed-in requester with no admin role.", $"/login?as=requester&returnUrl={encodedReturnUrl}")}}
                {{BuildDevLoginOption("Login as Unauthorized User", "Signs in without app roles so you can verify unauthorized states.", $"/login?as=unauthorized&returnUrl={encodedReturnUrl}")}}
                {{BuildDevLoginOption("Login as Self", "Runs the normal Entra sign-in flow with your real account.", $"/login?as=self&returnUrl={encodedReturnUrl}")}}
              </div>
              <div style="display: flex; flex-wrap: wrap; gap: 12px; margin-top: 28px;">
                <a href="{{safeReturnUrlText}}" style="display: inline-flex; align-items: center; justify-content: center; border-radius: 999px; background: #0f4c81; color: #ffffff; text-decoration: none; font-weight: 700; padding: 12px 18px;">
                  Continue to {{safeReturnUrlText}}
                </a>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;

        return Content(html, "text/html");
    }

    private static string BuildDevLoginOption(string label, string description, string href)
    {
        return $$"""
        <a href="{{WebUtility.HtmlEncode(href)}}" style="display: block; border-radius: 18px; border: 1px solid #dbe4f0; background: #ffffff; padding: 18px 20px; text-decoration: none; color: inherit;">
          <div style="font-size: 18px; font-weight: 700; color: #111827;">{{WebUtility.HtmlEncode(label)}}</div>
          <div style="margin-top: 6px; font-size: 14px; line-height: 1.5; color: #4b5563;">{{WebUtility.HtmlEncode(description)}}</div>
        </a>
        """;
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
