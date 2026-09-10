using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;
using Server.Services;

namespace Server.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SystemController : Controller
{
    private readonly AppDbContext _db;
    private readonly IUserService _userService;
    private readonly IAntiforgery _antiforgery;

    public SystemController(AppDbContext db, IUserService userService, IAntiforgery antiforgery)
    {
        _db = db;
        _userService = userService;
        _antiforgery = antiforgery;
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/system/emulate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Emulate()
    {
        return RenderEmulationPage();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/system/emulate")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Emulate([FromForm] string? identifier, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = NullIfWhiteSpace(identifier)?.ToLowerInvariant();
        if (normalizedIdentifier == null)
        {
            return RenderEmulationPage(identifier, "A user identifier is required.");
        }

        if (User.HasClaim(claim => claim.Type == AuthenticationHelper.EmulatingUserClaimType))
        {
            return RenderEmulationPage(identifier, "You are already emulating a user.");
        }

        var people = await _db.People
            .Where(person => person.IamId.Trim().ToLower() == normalizedIdentifier ||
                (person.EmployeeId != null && person.EmployeeId.Trim().ToLower() == normalizedIdentifier) ||
                (person.Email != null && person.Email.Trim().ToLower() == normalizedIdentifier) ||
                (person.UserId != null && person.UserId.Trim().ToLower() == normalizedIdentifier))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (people.Count == 0)
        {
            return RenderEmulationPage(identifier, "User not found in the People table.");
        }

        if (people.Count > 1)
        {
            return RenderEmulationPage(identifier, "Multiple people match that identifier. Use the user's IAM ID.");
        }

        var person = people[0];
        var iamId = person.IamId.Trim();
        var matchingUsers = _db.AppUsers
            .Where(user => user.IamId.Trim() == iamId)
            .OrderByDescending(user => user.UpdatedUtc)
            .ThenByDescending(user => user.Id);
        var user = await matchingUsers.FirstOrDefaultAsync(cancellationToken);
        if (user == null)
        {
            var now = DateTime.UtcNow;
            user = new AppUser
            {
                EntraObjectId = Guid.NewGuid(),
                IamId = iamId,
                EmployeeId = NullIfWhiteSpace(person.EmployeeId),
                DisplayName = NullIfWhiteSpace(person.FullName) ?? NullIfWhiteSpace(person.UserId)
                    ?? NullIfWhiteSpace(person.Email) ?? iamId,
                Email = NullIfWhiteSpace(person.Email),
                FirstLoginUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
            };
            _db.AppUsers.Add(user);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateAppUser(ex))
            {
                _db.Entry(user).State = EntityState.Detached;
                user = await matchingUsers.FirstOrDefaultAsync(cancellationToken);
                if (user == null)
                {
                    Response.StatusCode = StatusCodes.Status409Conflict;
                    return RenderEmulationPage(identifier,
                        "An AppUser with that employee ID or identity already exists for another person.");
                }
            }
        }

        var userId = user.EntraObjectId.ToString();
        var displayName = NullIfWhiteSpace(user.DisplayName) ?? NullIfWhiteSpace(person.FullName)
            ?? NullIfWhiteSpace(person.UserId) ?? iamId;
        var claims = new List<Claim>
        {
            new(ClaimConstants.ObjectId, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new("name", displayName),
            new("ucdPersonIAMID", user.IamId.Trim()),
            new(AuthenticationHelper.EmulatingUserClaimType, User.GetUserId()),
        };
        var email = NullIfWhiteSpace(person.Email) ?? NullIfWhiteSpace(user.Email);
        if (email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim("preferred_username", email));
        }

        var roles = await _userService.GetRolesForUser(userId);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return LocalRedirect("/");
    }

    [AllowAnonymous]
    [HttpGet("/system/endemulate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> EndEmulate()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/login");
    }

    private ContentResult RenderEmulationPage(string? identifier = null, string? error = null)
    {
        var encoder = HtmlEncoder.Default;
        var errorMarkup = error == null
            ? string.Empty
            : $"""<p role="alert" style="border: 1px solid #fecaca; border-radius: 8px; background: #fef2f2; color: #991b1b; padding: 16px;">{encoder.Encode(error)}</p>""";
        string formMarkup;
        if (User.HasClaim(claim => claim.Type == AuthenticationHelper.EmulatingUserClaimType))
        {
            formMarkup = """
                <p>You are emulating a user. End emulation before choosing another user.</p>
                <a href="/system/endemulate">End emulation</a>
                """;
        }
        else
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            formMarkup = $"""
                <form method="post" action="/system/emulate" style="display: grid; gap: 16px; margin-top: 24px;">
                  <input type="hidden" name="{encoder.Encode(tokens.FormFieldName)}" value="{encoder.Encode(tokens.RequestToken!)}">
                  <label for="identifier" style="font-weight: 700;">User identifier</label>
                  <input id="identifier" name="identifier" type="search" value="{encoder.Encode(identifier ?? string.Empty)}" required autofocus aria-describedby="identifier-help" style="width: 100%; box-sizing: border-box; border: 1px solid #6b7280; border-radius: 8px; padding: 12px; font: inherit;">
                  <p id="identifier-help" style="margin: 0; color: #4b5563; line-height: 1.5;">Enter the user's IAM ID, employee ID, email address, or login ID.</p>
                  <button type="submit" style="border: 0; border-radius: 8px; background: #022851; color: #ffffff; padding: 12px 18px; font: inherit; font-weight: 700; cursor: pointer;">Emulate user</button>
                </form>
                """;
        }

        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Emulate user | Leaves</title>
            </head>
            <body style="margin: 0; background: #f5f7fb; color: #1f2937; font-family: Arial, sans-serif;">
              <main style="min-height: 100vh; box-sizing: border-box; display: flex; align-items: center; justify-content: center; padding: 32px 16px;">
                <section aria-labelledby="emulate-title" style="width: 100%; max-width: 560px; box-sizing: border-box; border-radius: 16px; border: 1px solid #dbe4f0; background: #ffffff; padding: 32px;">
                  <a href="/" style="color: #022851;">Back to Leaves</a>
                  <h1 id="emulate-title" style="color: #022851;">Emulate user</h1>
                  <p style="line-height: 1.5;">Use Leaves with the selected user's access and permissions.</p>
                  {{errorMarkup}}
                  {{formMarkup}}
                </section>
              </main>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }

    private static bool IsDuplicateAppUser(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
