using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Server.Core.Data;

namespace Server.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string DevPersonaClaimType = "dev_persona";
    private static readonly HtmlEncoder HtmlEncoder = HtmlEncoder.Default;

    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _db;

    public AccountController(IWebHostEnvironment environment, AppDbContext db)
    {
        _environment = environment;
        _db = db;
    }

    [HttpGet("login")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Login(
        [FromQuery] string? returnUrl,
        [FromQuery(Name = "as")] string? asOption)
    {
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);

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
            "admin" => await SignInAsSeededDevPersonaAsync(
                displayNameFallback: DevelopmentSeedData.LocalAdminDisplayName,
                email: DevelopmentSeedData.LocalAdminEmail,
                iamIdFallback: DevelopmentSeedData.LocalAdminIamId,
                safeReturnUrl: safeReturnUrl,
                roles: ["Admin"]),
            "faculty" or "requester" => await SignInAsSeededDevPersonaAsync(
                displayNameFallback: DevelopmentSeedData.LocalRequesterDisplayName,
                email: DevelopmentSeedData.LocalRequesterEmail,
                iamIdFallback: DevelopmentSeedData.LocalRequesterIamId,
                safeReturnUrl: safeReturnUrl,
                roles: ["Faculty"]),
            "unauthorized" => await SignInAsSeededDevPersonaAsync(
                displayNameFallback: DevelopmentSeedData.LocalUnauthorizedDisplayName,
                email: DevelopmentSeedData.LocalUnauthorizedEmail,
                iamIdFallback: DevelopmentSeedData.LocalUnauthorizedIamId,
                safeReturnUrl: safeReturnUrl,
                roles: []),
            _ => RenderDevLoginPage(safeReturnUrl, $"Unknown login option '{asOption}'."),
        };
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        var trimmed = returnUrl.Trim();

        if (!trimmed.StartsWith('/'))
        {
            return "/";
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
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
        var safeReturnUrlText = HtmlEncoder.Encode(safeReturnUrl);
        var errorMarkup = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""
              <div style="margin-top: 24px; border-radius: 16px; border: 1px solid #fecaca; background: #fef2f2; color: #991b1b; padding: 16px 18px; font-size: 14px;">
                {HtmlEncoder.Encode(error)}
              </div>
              """;

        var authenticatedMarkup = User.Identity?.IsAuthenticated == true
            ? $"""
              <div style="margin-top: 24px;">
                <p style="margin: 0 0 12px; color: #4b5563; font-size: 14px;">
                  Currently signed in as <strong>{HtmlEncoder.Encode(User.Identity?.Name ?? "(unknown)")}</strong>.
                </p>
                <a href="{safeReturnUrlText}" style="display: inline-flex; align-items: center; justify-content: center; border-radius: 999px; background: #0f4c81; color: #ffffff; text-decoration: none; font-weight: 700; padding: 12px 18px;">
                  Continue to {safeReturnUrlText}
                </a>
              </div>
              """
            : $"""
              <div style="display: flex; flex-wrap: wrap; gap: 12px; margin-top: 28px;">
                <a href="{safeReturnUrlText}" style="display: inline-flex; align-items: center; justify-content: center; border-radius: 999px; background: #0f4c81; color: #ffffff; text-decoration: none; font-weight: 700; padding: 12px 18px;">
                  Continue to {safeReturnUrlText}
                </a>
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
                <a href="/login?as=admin&returnUrl={{encodedReturnUrl}}" style="display: block; border-radius: 18px; border: 1px solid #dbe4f0; background: #ffffff; padding: 18px 20px; text-decoration: none; color: inherit;">
                  <div style="font-size: 18px; font-weight: 700; color: #111827;">Login as Admin</div>
                  <div style="margin-top: 6px; font-size: 14px; line-height: 1.5; color: #4b5563;">Grants the local Admin role for testing admin-only UI.</div>
                </a>
                <a href="/login?as=faculty&returnUrl={{encodedReturnUrl}}" style="display: block; border-radius: 18px; border: 1px solid #dbe4f0; background: #ffffff; padding: 18px 20px; text-decoration: none; color: inherit;">
                  <div style="font-size: 18px; font-weight: 700; color: #111827;">Login as Faculty</div>
                  <div style="margin-top: 6px; font-size: 14px; line-height: 1.5; color: #4b5563;">Simulates a standard signed-in faculty member with no admin authority.</div>
                </a>
                <a href="/login?as=unauthorized&returnUrl={{encodedReturnUrl}}" style="display: block; border-radius: 18px; border: 1px solid #dbe4f0; background: #ffffff; padding: 18px 20px; text-decoration: none; color: inherit;">
                  <div style="font-size: 18px; font-weight: 700; color: #111827;">Login as Unauthorized User</div>
                  <div style="margin-top: 6px; font-size: 14px; line-height: 1.5; color: #4b5563;">Signs in without app roles so you can verify unauthorized states.</div>
                </a>
                <a href="/login?as=self&returnUrl={{encodedReturnUrl}}" style="display: block; border-radius: 18px; border: 1px solid #dbe4f0; background: #ffffff; padding: 18px 20px; text-decoration: none; color: inherit;">
                  <div style="font-size: 18px; font-weight: 700; color: #111827;">Login as Self</div>
                  <div style="margin-top: 6px; font-size: 14px; line-height: 1.5; color: #4b5563;">Runs the normal Entra sign-in flow with your real account.</div>
                </a>
              </div>
              {{authenticatedMarkup}}
            </section>
          </main>
        </body>
        </html>
        """;

        return Content(html, "text/html");
    }

    private async Task<IActionResult> SignInAsSeededDevPersonaAsync(
        string displayNameFallback,
        string email,
        string iamIdFallback,
        string safeReturnUrl,
        IReadOnlyCollection<string> roles)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedIamId = iamIdFallback.Trim();

        var user = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(user =>
                user.Email != null && user.Email.ToLower() == normalizedEmail);

        if (user is null)
        {
            user = await _db.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.IamId == normalizedIamId);
        }

        if (user is null)
        {
            return RenderDevLoginPage(
                safeReturnUrl,
                $"User '{email}' not found in the local database.");
        }

        var displayName = displayNameFallback;
        var resolvedEmail = email;

        var claims = new List<Claim>
        {
            new(ClaimConstants.ObjectId, user.EntraObjectId.ToString()),
            new(ClaimTypes.NameIdentifier, user.EntraObjectId.ToString()),
            new(ClaimTypes.Name, displayName),
            new("name", displayName),
            new(ClaimTypes.Email, resolvedEmail),
            new("preferred_username", resolvedEmail),
            new(DevPersonaClaimType, "true"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        return Redirect(safeReturnUrl);
    }
}
