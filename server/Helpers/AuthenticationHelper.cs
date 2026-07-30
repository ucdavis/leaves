using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Server.Services;

namespace Server.Helpers;

public static class AuthenticationHelper
{
    /// <summary>
    /// Configures Microsoft Identity Web authentication with Azure AD/Entra ID
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
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
                OnRedirectToAccessDenied = ctx =>
                {
                    // If the request is for an API endpoint, don't redirect to the access denied page
                    if (ctx.Request.Path.StartsWithSegments("/api"))
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
    /// Handles token validation by synchronizing the user profile at sign-in.
    /// </summary>
    private static async Task OnTokenValidated(Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext ctx)
    {
        var principal = ctx.Principal;
        if (principal == null)
        {
            return;
        }

        var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
        if (!principal.TryGetUserId(out _))
        {
            return;
        }

        await userService.EnsureUserProfileAsync(
            principal,
            recordSignIn: true,
            cancellationToken: ctx.HttpContext.RequestAborted);
    }
}

public sealed class AdminAuthorizationRequirement : IAuthorizationRequirement;

public sealed class AdminAuthorizationHandler(IUserService userService)
    : AuthorizationHandler<AdminAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminAuthorizationRequirement requirement)
    {
        if (context.User.HasClaim("dev_persona", "true"))
        {
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (!context.User.TryGetUserId(out var userId))
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted
            ?? CancellationToken.None;
        var roles = await userService.GetRolesForUser(userId, cancellationToken);

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
