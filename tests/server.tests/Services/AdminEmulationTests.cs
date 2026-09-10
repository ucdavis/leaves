using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Server.Controllers;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;
using Server.Services;

namespace Server.Tests.Services;

public class SystemEmulationTests
{
    [Fact]
    public void Emulate_requires_the_admin_policy_for_get_and_post_without_caching()
    {
        var controllerType = typeof(SystemController);
        controllerType.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        controllerType.GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
        var get = controllerType.GetMethod(nameof(SystemController.Emulate), Type.EmptyTypes)!;
        var post = controllerType.GetMethod(nameof(SystemController.Emulate), [typeof(string), typeof(CancellationToken)])!;

        get.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("/system/emulate");
        get.GetCustomAttribute<HttpPostAttribute>().Should().BeNull();
        post.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("/system/emulate");
        post.GetCustomAttribute<HttpGetAttribute>().Should().BeNull();
        post.GetParameters().Single(parameter => parameter.Name == "identifier")
            .GetCustomAttribute<FromFormAttribute>().Should().NotBeNull();
        post.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull();
        foreach (var action in new[] { get, post })
        {
            action.GetCustomAttributes<AuthorizeAttribute>()
                .Should().Contain(attribute => attribute.Policy == "AdminOnly");
            action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
            var cache = action.GetCustomAttribute<ResponseCacheAttribute>()!;
            cache.NoStore.Should().BeTrue();
            cache.Location.Should().Be(ResponseCacheLocation.None);
        }
    }

    [Fact]
    public void Emulate_get_renders_a_post_form_with_antiforgery_without_creating_or_signing_in_a_user()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var antiforgery = new RecordingAntiforgery();
        var (controller, authentication) = CreateController(db, antiforgery: antiforgery);

        var result = controller.Emulate();

        var page = result.Should().BeOfType<ContentResult>().Which;
        page.ContentType.Should().StartWith("text/html");
        page.Content.Should().MatchRegex("<form\\b[^>]*action=\"/system/emulate\"");
        page.Content.Should().MatchRegex("<form\\b[^>]*method=\"post\"");
        page.Content.Should().MatchRegex("<input\\b[^>]*name=\"identifier\"");
        page.Content.Should().MatchRegex("<input\\b(?=[^>]*type=\"hidden\")(?=[^>]*name=\"__RequestVerificationToken\")[^>]*value=\"request-token\"");
        antiforgery.TokenContext.Should().BeSameAs(controller.HttpContext);
        db.AppUsers.Should().BeEmpty();
        authentication.Principal.Should().BeNull();
        authentication.SignOutScheme.Should().BeNull();
    }

    [Theory]
    [InlineData(" IAM123 ")]
    [InlineData(" EMP123 ")]
    [InlineData(" TARGET@EXAMPLE.COM ")]
    [InlineData(" TARGET ")]
    public async Task Emulate_matches_each_people_identifier_ignoring_case_and_padding(string identifier)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var target = AddAppUser(db);
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);

        var result = await controller.Emulate(identifier, default);

        result.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/");
        authentication.Scheme.Should().Be(CookieAuthenticationDefaults.AuthenticationScheme);
        authentication.Principal!.GetUserId().Should().Be(target.EntraObjectId.ToString());
        db.AppUsers.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null, "A user identifier is required.")]
    [InlineData("   ", "A user identifier is required.")]
    [InlineData("missing", "User not found in the People table.")]
    public async Task Emulate_returns_content_without_creating_or_signing_in_an_unknown_user(
        string? identifier,
        string expectedContent)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var (controller, authentication) = CreateController(db);

        var result = await controller.Emulate(identifier, default);

        var page = result.Should().BeOfType<ContentResult>().Which;
        page.ContentType.Should().StartWith("text/html");
        WebUtility.HtmlDecode(page.Content).Should().Contain(expectedContent);
        db.AppUsers.Should().BeEmpty();
        authentication.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Emulate_refuses_an_identifier_shared_by_multiple_people()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        db.Set<Person>().Add(new Person { IamId = "otheriam", UserId = "target" });
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);

        var result = await controller.Emulate("target", default);

        var page = result.Should().BeOfType<ContentResult>().Which;
        WebUtility.HtmlDecode(page.Content)
            .Should().Contain("Multiple people match that identifier. Use the user's IAM ID.");
        db.AppUsers.Should().BeEmpty();
        authentication.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Emulate_refuses_to_replace_an_existing_emulation()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        await db.SaveChangesAsync();
        var principal = CreatePrincipal(Guid.NewGuid(), "adminiam");
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim(AuthenticationHelper.EmulatingUserClaimType, Guid.NewGuid().ToString()));
        var (controller, authentication) = CreateController(db, principal);

        var result = await controller.Emulate("iam123", default);

        var page = result.Should().BeOfType<ContentResult>().Which;
        WebUtility.HtmlDecode(page.Content).Should().Contain("You are already emulating a user.");
        db.AppUsers.Should().BeEmpty();
        authentication.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Emulate_post_preserves_an_encoded_search_value_and_antiforgery_token_for_retry()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var antiforgery = new RecordingAntiforgery();
        var (controller, authentication) = CreateController(db, antiforgery: antiforgery);
        const string identifier = "\"><script>alert(\"x\")</script>&";

        var result = await controller.Emulate(identifier, default);

        var page = result.Should().BeOfType<ContentResult>().Which;
        page.ContentType.Should().StartWith("text/html");
        page.Content.Should().Contain("User not found in the People table.");
        page.Content.Should().MatchRegex("<form\\b[^>]*action=\"/system/emulate\"");
        page.Content.Should().MatchRegex("<form\\b[^>]*method=\"post\"");
        page.Content.Should().Contain("value=\"&quot;&gt;&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;&amp;\"");
        page.Content.Should().NotContain(identifier);
        page.Content.Should().MatchRegex("<input\\b(?=[^>]*type=\"hidden\")(?=[^>]*name=\"__RequestVerificationToken\")[^>]*value=\"request-token\"");
        antiforgery.TokenContext.Should().BeSameAs(controller.HttpContext);
        db.AppUsers.Should().BeEmpty();
        authentication.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Emulate_uses_the_most_recently_updated_app_user_for_the_IAM_id()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var newest = AddAppUser(db);
        newest.UpdatedUtc = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var older = AddAppUser(db);
        older.IamId = " iam123 ";
        older.UpdatedUtc = newest.UpdatedUtc.AddDays(-1);
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);

        await controller.Emulate("iam123", default);

        authentication.Principal!.GetUserId().Should().Be(newest.EntraObjectId.ToString());
        db.AppUsers.Should().HaveCount(2);
        newest.UpdatedUtc.Should().Be(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Emulate_creates_an_app_user_with_a_new_guid_and_people_profile()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        await db.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        var (controller, authentication) = CreateController(db, CreatePrincipal(actorId, "adminiam"));
        var before = DateTime.UtcNow;

        await controller.Emulate("iam123", default);

        var target = db.AppUsers.Should().ContainSingle().Which;
        target.EntraObjectId.Should().NotBeEmpty().And.NotBe(actorId);
        target.IamId.Should().Be("iam123");
        target.EmployeeId.Should().Be("emp123");
        target.DisplayName.Should().Be("Target Person");
        target.Email.Should().Be("target@example.com");
        target.IsActive.Should().BeTrue();
        target.CreatedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        target.UpdatedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        target.FirstLoginUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        target.LastLoginUtc.Should().BeNull();
        var principal = authentication.Principal!;
        principal.GetUserId().Should().Be(target.EntraObjectId.ToString());
        principal.FindFirstValue("ucdPersonIAMID").Should().Be(target.IamId);
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(target.EntraObjectId.ToString());
        principal.FindFirstValue("name").Should().Be(target.DisplayName);
        principal.FindFirstValue(ClaimTypes.Name).Should().Be(target.DisplayName);
        principal.FindFirstValue(ClaimTypes.Email).Should().Be(target.Email);
        principal.FindFirstValue("preferred_username").Should().Be(target.Email);
        principal.FindFirstValue(AuthenticationHelper.EmulatingUserClaimType)
            .Should().Be(actorId.ToString());
    }

    [Fact]
    public async Task Emulate_uses_only_the_targets_roles_and_does_not_copy_the_admin_or_dev_claims()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var target = AddAppUser(db);
        AddCaoAssignment(db, target);
        await db.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        var actor = CreatePrincipal(actorId, "adminiam");
        ((ClaimsIdentity)actor.Identity!).AddClaims([
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("dev_persona", "true"),
            new Claim("actor_only", "actor value"),
        ]);
        var (controller, authentication) = CreateController(db, actor);

        await controller.Emulate("iam123", default);

        authentication.Principal!.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Should().BeEquivalentTo(["CAO"]);
        authentication.Principal.HasClaim(claim => claim.Type == "dev_persona").Should().BeFalse();
        authentication.Principal.HasClaim(claim => claim.Type == "actor_only").Should().BeFalse();
        authentication.Principal.GetUserId().Should().Be(target.EntraObjectId.ToString());
        authentication.Principal.FindFirstValue(AuthenticationHelper.EmulatingUserClaimType)
            .Should().Be(actorId.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Me_reports_whether_the_current_user_is_emulated(bool isEmulating)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var target = AddAppUser(db);
        await db.SaveChangesAsync();
        var principal = CreatePrincipal(target.EntraObjectId, target.IamId);
        if (isEmulating)
        {
            ((ClaimsIdentity)principal.Identity!).AddClaim(
                new Claim(AuthenticationHelper.EmulatingUserClaimType, Guid.NewGuid().ToString()));
        }
        var controller = new UserController(CreateUserService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

        var response = (await controller.Me()).Should().BeOfType<OkObjectResult>().Which;

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        document.RootElement.GetProperty("IsEmulating").GetBoolean().Should().Be(isEmulating);
        document.RootElement.GetProperty("Name").GetString().Should().Be(target.DisplayName);
    }

    [Fact]
    public async Task EnsureUserProfileAsync_preserves_the_emulated_identity_when_another_person_shares_the_email()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var target = AddAppUser(db);
        target.UpdatedUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedUtc = target.UpdatedUtc;
        db.Set<Person>().Add(new Person
        {
            IamId = "otheriam",
            EmployeeId = "otheremp",
            Email = "target@example.com",
            FullName = "Another Person",
            PromotedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);
        await controller.Emulate("iam123", default);

        var provisioned = await CreateUserService(db).EnsureUserProfileAsync(
            authentication.Principal!, recordSignIn: false);

        provisioned.Should().BeTrue();
        target.IamId.Should().Be("iam123");
        target.EmployeeId.Should().Be("emp123");
        target.DisplayName.Should().Be("Target Person");
        target.UpdatedUtc.Should().Be(updatedUtc);
        db.AppUsers.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnsureUserProfileAsync_rejects_emulation_without_the_matching_app_user(bool userExists)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var userId = Guid.NewGuid();
        if (userExists)
        {
            var target = AddAppUser(db);
            target.EntraObjectId = userId;
        }
        await db.SaveChangesAsync();
        var principal = CreatePrincipal(userId, userExists ? "otheriam" : "iam123");
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim(AuthenticationHelper.EmulatingUserClaimType, Guid.NewGuid().ToString()));

        var provisioned = await CreateUserService(db).EnsureUserProfileAsync(principal, recordSignIn: false);

        provisioned.Should().BeFalse();
        db.AppUsers.Should().HaveCount(userExists ? 1 : 0);
        if (userExists)
        {
            db.AppUsers.Single().IamId.Should().Be("iam123");
        }
    }

    [Fact]
    public async Task The_first_real_sign_in_reuses_the_emulation_created_app_user_with_the_real_object_id()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);
        await controller.Emulate("iam123", default);
        var emulatedUser = db.AppUsers.Single();
        var appUserId = emulatedUser.Id;
        var emulationObjectId = emulatedUser.EntraObjectId;
        var actualObjectId = Guid.NewGuid();
        var actualPrincipal = CreatePrincipal(actualObjectId, emulatedUser.IamId);
        var before = DateTime.UtcNow;

        var provisioned = await CreateUserService(db).EnsureUserProfileAsync(actualPrincipal, recordSignIn: true);

        provisioned.Should().BeTrue();
        var realUser = db.AppUsers.Should().ContainSingle().Which;
        realUser.Id.Should().Be(appUserId);
        realUser.EntraObjectId.Should().Be(actualObjectId).And.NotBe(emulationObjectId);
        realUser.IamId.Should().Be("iam123");
        realUser.LastLoginUtc.Should().NotBeNull();
        realUser.LastLoginUtc!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        (await CreateUserService(db).EnsureUserProfileAsync(authentication.Principal!, recordSignIn: false))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Role_refresh_preserves_the_emulation_marker_and_removes_revoked_roles()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        AddPerson(db);
        var target = AddAppUser(db);
        var assignment = AddCaoAssignment(db, target);
        await db.SaveChangesAsync();
        var (controller, authentication) = CreateController(db);
        await controller.Emulate("iam123", default);
        var originalMarker = authentication.Principal!.FindFirstValue(AuthenticationHelper.EmulatingUserClaimType);
        assignment.ClosedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var refreshed = await CreateUserService(db).UpdateUserPrincipalIfNeeded(authentication.Principal!);

        refreshed.Should().NotBeNull();
        refreshed!.FindAll(ClaimTypes.Role).Should().BeEmpty();
        refreshed.GetUserId().Should().Be(target.EntraObjectId.ToString());
        refreshed.FindFirstValue(AuthenticationHelper.EmulatingUserClaimType).Should().Be(originalMarker);
    }

    [Fact]
    public void EndEmulate_is_accessible_without_admin_access_and_does_not_cache()
    {
        var controllerType = typeof(SystemController);
        var action = controllerType.GetMethod(nameof(SystemController.EndEmulate))!;

        action.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("/system/endemulate");
        action.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        controllerType.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
            .Should().NotContain(attribute => attribute.Policy == "AdminOnly");
        var cache = action.GetCustomAttribute<ResponseCacheAttribute>()!;
        cache.NoStore.Should().BeTrue();
        cache.Location.Should().Be(ResponseCacheLocation.None);
    }

    [Theory]
    [InlineData(true, "Faculty")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public async Task EndEmulate_clears_the_cookie_and_redirects_to_login_without_restoring_an_actor(
        bool isAuthenticated,
        string? role)
    {
        var principal = isAuthenticated
            ? CreatePrincipal(Guid.NewGuid(), "iam123")
            : new ClaimsPrincipal(new ClaimsIdentity());
        var identity = (ClaimsIdentity)principal.Identity!;
        if (isAuthenticated)
        {
            identity.AddClaim(new Claim(AuthenticationHelper.EmulatingUserClaimType, Guid.NewGuid().ToString()));
        }
        if (role != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        using var db = TestDbContextFactory.CreateInMemory();
        var (controller, authentication) = CreateController(db, principal);

        var result = await controller.EndEmulate();

        authentication.SignOutScheme.Should().Be(CookieAuthenticationDefaults.AuthenticationScheme);
        authentication.Principal.Should().BeNull();
        result.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/login");
    }

    private static void AddPerson(AppDbContext db) => db.Set<Person>().Add(new Person
    {
        IamId = "iam123   ",
        EmployeeId = "emp123  ",
        Email = "target@example.com ",
        FullName = "Target Person",
        UserId = "target  ",
    });

    private static AppUser AddAppUser(AppDbContext db)
    {
        var user = new AppUser
        {
            IamId = "iam123",
            EmployeeId = "emp123",
            EntraObjectId = Guid.NewGuid(),
            DisplayName = "Target Person",
            Email = "target@example.com",
            FirstLoginUtc = DateTime.UtcNow,
        };
        db.AppUsers.Add(user);
        return user;
    }

    private static ClusterCaoAssignment AddCaoAssignment(AppDbContext db, AppUser user)
    {
        var assignment = new ClusterCaoAssignment
        {
            Cluster = new Cluster { ClusterName = "Test Cluster" },
            CreatedByAppUser = user,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            IamId = user.IamId,
        };
        db.ClusterCaoAssignments.Add(assignment);
        return assignment;
    }

    private static UserService CreateUserService(AppDbContext db) =>
        new(NullLogger<UserService>.Instance, db);

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string iamId) =>
        new(new ClaimsIdentity([
            new Claim(ClaimConstants.ObjectId, userId.ToString()),
            new Claim("ucdPersonIAMID", iamId),
            new Claim("preferred_username", "target@example.com"),
            new Claim("name", "Original Name"),
        ], CookieAuthenticationDefaults.AuthenticationScheme));

    private static (SystemController Controller, RecordingAuthenticationService Authentication) CreateController(
        AppDbContext db,
        ClaimsPrincipal? principal = null,
        RecordingAntiforgery? antiforgery = null)
    {
        var authentication = new RecordingAuthenticationService();
        var controller = new SystemController(db, CreateUserService(db), antiforgery ?? new RecordingAntiforgery())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = authentication,
                    User = principal ?? CreatePrincipal(Guid.NewGuid(), "adminiam"),
                },
            },
        };
        return (controller, authentication);
    }

    private sealed class RecordingAntiforgery : IAntiforgery
    {
        public HttpContext? TokenContext { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            TokenContext = httpContext;
            return new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "RequestVerificationToken");
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            throw new NotSupportedException();

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) =>
            throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext) =>
            throw new NotSupportedException();

        public void SetCookieTokenAndHeader(HttpContext httpContext) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService, IServiceProvider
    {
        public ClaimsPrincipal? Principal { get; private set; }
        public string? Scheme { get; private set; }
        public string? SignOutScheme { get; private set; }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IAuthenticationService) ? this : null;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            Principal = principal;
            Scheme = scheme;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            throw new NotSupportedException();

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutScheme = scheme;
            return Task.CompletedTask;
        }
    }
}
