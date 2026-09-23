using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Server.Services;

namespace Server.Helpers;

public static class AuthenticationHelper
{
    public const string EmulatingUserClaimType = "emulating_user";
    private const string PrincipalRefreshTicksClaimType = "leaves_principal_refresh_ticks";

    /// <summary>
    /// Configures Microsoft Identity Web authentication with Azure AD/Entra ID
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthenticationRefreshOptions>()
            .Bind(configuration.GetSection("Authentication"))
            .Validate(
                options => options.PrincipalRefreshIntervalMinutes >= 1 &&
                           options.PrincipalRefreshIntervalMinutes <= 60,
                "Authentication:PrincipalRefreshIntervalMinutes must be between 1 and 60.")
            .ValidateOnStart();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(options =>
            {
                configuration.Bind("Auth", options);

                options.TokenValidationParameters = new()
                {
                    NameClaimType = "name",
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events ??= new OpenIdConnectEvents();
                options.Events.OnRedirectToIdentityProvider = OnRedirectToIdentityProvider;
                options.Events.OnTokenValidated = OnTokenValidated;
            });

        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = OnValidatePrincipal,
                OnRedirectToAccessDenied = ctx =>
                {
                    // API and system endpoints report denied access directly.
                    if (ctx.Request.Path.StartsWithSegments("/api") ||
                        ctx.Request.Path.StartsWithSegments("/system"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Handles redirect to identity provider - prevents API endpoints from redirecting to login page
    /// </summary>
    private static Task OnRedirectToIdentityProvider(Microsoft.AspNetCore.Authentication.OpenIdConnect.RedirectContext ctx)
    {
        // If the request is for an API endpoint, don't redirect to the login page
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = 401;
            ctx.HandleResponse();
            return Task.CompletedTask;
        }

        // Set domain hint for UC Davis
        ctx.ProtocolMessage.DomainHint = "ucdavis.edu";

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles token validation - loads user roles on first login
    /// </summary>
    private static async Task OnTokenValidated(Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext ctx)
    {
        // Load up the roles on first login (can also change other user info/claims here if needed)
        var principal = ctx.Principal;
        if (principal == null)
        {
            return;
        }

        var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
        var hasUserId = principal.TryGetUserId(out var userId);

        if (!hasUserId)
        {
            ctx.Fail("Your account is missing a required object ID. Contact support.");
            return;
        }

        var profileProvisioned = await userService.EnsureUserProfileAsync(
            principal,
            recordSignIn: true,
            cancellationToken: ctx.HttpContext.RequestAborted);
        if (!profileProvisioned)
        {
            ctx.Fail("Your account could not be matched to an IAM ID. Contact support.");
            return;
        }

        var roles = await userService.GetRolesForUser(userId);

        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        ReplacePrincipalRefreshClaim(identity, DateTime.UtcNow);
    }

    /// <summary>
    /// Validates cookie principal on every request - updates user roles/claims if needed
    /// </summary>
    private static async Task OnValidatePrincipal(Microsoft.AspNetCore.Authentication.Cookies.CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        if (principal?.HasClaim("dev_persona", "true") == true)
        {
            return;
        }

        if (principal == null)
        {
            return;
        }

        var refreshOptions = ctx.HttpContext.RequestServices
            .GetRequiredService<IOptions<AuthenticationRefreshOptions>>()
            .Value;
        var now = DateTime.UtcNow;
        if (!RequiresPrincipalRefresh(principal, now, refreshOptions.PrincipalRefreshInterval))
        {
            return;
        }

        // Profile and role queries are intentionally limited to this refresh interval. Role-changing
        // operations are still observed on the next refresh without adding database work to every request.
        var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
        var profileProvisioned = await userService.EnsureUserProfileAsync(
            principal,
            recordSignIn: false,
            cancellationToken: ctx.HttpContext.RequestAborted);
        if (!profileProvisioned)
        {
            ctx.RejectPrincipal();
            return;
        }

        var updated = await userService.UpdateUserPrincipalIfNeeded(principal);

        var refreshedPrincipal = updated ?? principal;
        ctx.ReplacePrincipal(WithPrincipalRefreshClaim(refreshedPrincipal, now));
        ctx.ShouldRenew = true;
    }

    private static bool RequiresPrincipalRefresh(
        ClaimsPrincipal principal,
        DateTime now,
        TimeSpan interval)
    {
        var refreshClaim = principal.FindFirst(PrincipalRefreshTicksClaimType)?.Value;
        if (!long.TryParse(refreshClaim, out var refreshTicks) ||
            refreshTicks < DateTime.MinValue.Ticks ||
            refreshTicks > now.Ticks)
        {
            return true;
        }

        return now - new DateTime(refreshTicks, DateTimeKind.Utc) >= interval;
    }

    private static ClaimsPrincipal WithPrincipalRefreshClaim(ClaimsPrincipal principal, DateTime refreshedAt)
    {
        var identities = principal.Identities
            .Select(identity => new ClaimsIdentity(identity))
            .ToList();
        var identity = identities.FirstOrDefault(identity => identity.IsAuthenticated);
        if (identity == null)
        {
            identity = new ClaimsIdentity(principal.Identity);
            identities.Add(identity);
        }

        foreach (var existingIdentity in identities)
        {
            foreach (var claim in existingIdentity.FindAll(PrincipalRefreshTicksClaimType).ToList())
            {
                existingIdentity.RemoveClaim(claim);
            }
        }

        ReplacePrincipalRefreshClaim(identity, refreshedAt);
        return new ClaimsPrincipal(identities);
    }

    private static void ReplacePrincipalRefreshClaim(ClaimsIdentity identity, DateTime refreshedAt)
    {
        foreach (var claim in identity.FindAll(PrincipalRefreshTicksClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(PrincipalRefreshTicksClaimType, refreshedAt.Ticks.ToString()));
    }
}

public sealed class AuthenticationRefreshOptions
{
    public int PrincipalRefreshIntervalMinutes { get; init; } = 5;

    public TimeSpan PrincipalRefreshInterval => TimeSpan.FromMinutes(PrincipalRefreshIntervalMinutes);
}
