using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Server.Core.Domain;
using Server.Services;
using Server.Tests;

namespace Server.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task EnsureUserProfileAsync_uses_the_People_table_to_resolve_employee_id_from_IamId()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person
        {
            IamId = "IAM123456",
            EmployeeId = "E12345",
            Email = "different-person@example.com",
            FullName = "IAM Match Person",
        });
        await db.SaveChangesAsync();

        var service = new UserService(NullLogger<UserService>.Instance, db);

        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(
            userId,
            email: "person@example.com",
            iamId: "IAM-123456");

        await service.EnsureUserProfileAsync(principal, cancellationToken: default);

        var user = db.AppUsers.Single();
        user.EntraObjectId.Should().Be(userId);
        user.Email.Should().Be("person@example.com");
        user.IamId.Should().Be("iam123456");
        user.EmployeeId.Should().Be("E12345");
    }

    [Fact]
    public async Task EnsureUserProfileAsync_returns_false_without_a_resolvable_IamId()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var service = new UserService(NullLogger<UserService>.Instance, db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimConstants.ObjectId, Guid.NewGuid().ToString())],
            "Cookies"));

        var profileProvisioned = await service.EnsureUserProfileAsync(principal);

        profileProvisioned.Should().BeFalse();
        db.AppUsers.Should().BeEmpty();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string email, string iamId)
    {
        var claims = new List<Claim>
        {
            new(ClaimConstants.ObjectId, userId.ToString()),
            new("preferred_username", email),
            new("ucdPersonIAMID", iamId),
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }
}
