using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Controllers;
using Server.Core.Domain;
using Server.Helpers;
using Server.Services;

namespace Server.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task AdminAuthorizationUsesCurrentDatabaseGrant()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var appUser = CreateAppUser();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        db.AppAdminAssignments.Add(new AppAdminAssignment
        {
            CreatedByAppUserId = appUser.Id,
            IamId = appUser.IamId,
        });
        await db.SaveChangesAsync();

        var handler = new AdminAuthorizationHandler(
            new UserService(NullLogger<UserService>.Instance, db));
        var context = CreateAuthorizationContext(CreatePrincipal(appUser.EntraObjectId));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task AdminAuthorizationIgnoresStaleCookieGrantAfterDatabaseRevocation()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var appUser = CreateAppUser();
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var handler = new AdminAuthorizationHandler(
            new UserService(NullLogger<UserService>.Instance, db));
        var principal = CreatePrincipal(
            appUser.EntraObjectId,
            new Claim(ClaimTypes.Role, "Admin"));
        var context = CreateAuthorizationContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task AdminAuthorizationUsesCookieRoleForDevelopmentPersonaWithoutDatabaseLookup()
    {
        var userService = new RecordingUserService(["Admin"]);
        var handler = new AdminAuthorizationHandler(userService);
        var principal = CreatePrincipal(
            Guid.NewGuid(),
            new Claim("dev_persona", "true"),
            new Claim(ClaimTypes.Role, "Admin"));
        var context = CreateAuthorizationContext(principal);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        userService.RoleLookupCount.Should().Be(0);
    }

    [Fact]
    public async Task UserEndpointReturnsCurrentDatabaseRolesInsteadOfStaleCookieRoles()
    {
        var userService = new RecordingUserService([]);
        var entraObjectId = Guid.NewGuid();
        var principal = CreatePrincipal(
            entraObjectId,
            new Claim("name", "Test User"),
            new Claim("preferred_username", "test@example.com"),
            new Claim(ClaimTypes.Role, "Admin"));
        var controller = CreateUserController(userService, principal);

        var result = await controller.Me(CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        GetResponseRoles(response).Should().BeEmpty();
        userService.RoleLookupCount.Should().Be(1);
    }

    [Fact]
    public async Task UserEndpointPreservesDevelopmentPersonaRolesWithoutDatabaseLookup()
    {
        var userService = new RecordingUserService(["Admin"]);
        var principal = CreatePrincipal(
            Guid.NewGuid(),
            new Claim("dev_persona", "true"),
            new Claim(ClaimTypes.Role, "User"));
        var controller = CreateUserController(userService, principal);

        var result = await controller.Me(CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        GetResponseRoles(response).Should().Equal("User");
        userService.RoleLookupCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRolesForUserSupportsIamIdWithoutAppUserLookup()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var creator = CreateAppUser();
        creator.IamId = "creator001";
        db.AppUsers.Add(creator);
        await db.SaveChangesAsync();

        db.AppAdminAssignments.Add(new AppAdminAssignment
        {
            CreatedByAppUserId = creator.Id,
            IamId = "admin00001",
        });
        await db.SaveChangesAsync();

        var service = new UserService(NullLogger<UserService>.Instance, db);

        var roles = await service.GetRolesForUser(" admin00001 ");

        roles.Should().Equal("Admin");
    }

    [Fact]
    public async Task EnsureUserProfileUsesLatestMatchingPersonWithoutTrackingAllMatches()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().AddRange(
            new Person
            {
                IamId = "0000000001",
                Email = "person@example.com",
                EmployeeId = "12345678",
                FullName = "Older Name",
                ModifyDate = new DateTime(2026, 1, 1),
                PromotedAt = new DateTime(2026, 1, 1),
            },
            new Person
            {
                IamId = "0000000002",
                Email = "person@example.com",
                EmployeeId = "87654321",
                FullName = "Current Name",
                ModifyDate = new DateTime(2026, 2, 1),
                PromotedAt = new DateTime(2026, 2, 1),
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new UserService(NullLogger<UserService>.Instance, db);
        var entraObjectId = Guid.NewGuid();
        var principal = CreatePrincipal(
            entraObjectId,
            new Claim("name", "Token Name"),
            new Claim("preferred_username", "person@example.com"));

        await service.EnsureUserProfileAsync(principal);

        var appUser = await db.AppUsers.SingleAsync();
        appUser.IamId.Should().Be("0000000002");
        appUser.EmployeeId.Should().Be("87654321");
        appUser.DisplayName.Should().Be("Current Name");
        db.ChangeTracker.Entries<Person>().Should().BeEmpty();
    }

    private static AppUser CreateAppUser()
    {
        return new AppUser
        {
            DisplayName = "Test Admin",
            Email = "admin@example.com",
            EntraObjectId = Guid.NewGuid(),
            FirstLoginUtc = DateTime.UtcNow,
            IamId = "admin00001",
        };
    }

    private static ClaimsPrincipal CreatePrincipal(
        Guid entraObjectId,
        params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, entraObjectId.ToString()),
        };
        claims.AddRange(additionalClaims);

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "TestAuth",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        ClaimsPrincipal principal)
    {
        var requirement = new AdminAuthorizationRequirement();
        return new AuthorizationHandlerContext(
            [requirement],
            principal,
            new DefaultHttpContext());
    }

    private static UserController CreateUserController(
        IUserService userService,
        ClaimsPrincipal principal)
    {
        return new UserController(userService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal,
                },
            },
        };
    }

    private static IReadOnlyCollection<string> GetResponseRoles(object response)
    {
        return (IReadOnlyCollection<string>)response.GetType()
            .GetProperty("Roles")!
            .GetValue(response)!;
    }

    private sealed class RecordingUserService(IReadOnlyCollection<string> roles)
        : IUserService
    {
        public int RoleLookupCount { get; private set; }

        public Task EnsureUserProfileAsync(
            ClaimsPrincipal principal,
            bool recordSignIn = true,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetDisplayNameForUser(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("Test User");
        }

        public Task<List<string>> GetRolesForUser(
            string userId,
            CancellationToken cancellationToken = default)
        {
            RoleLookupCount++;
            return Task.FromResult(roles.ToList());
        }
    }
}
