using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Server.Controllers;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class AdminRoleMembershipTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task Directory_membership_requires_employee_status_without_accruals_or_an_AppUser(
        bool? isEmployee,
        bool expected)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person
        {
            IamId = "employee01",
            IsEmployee = isEmployee,
            IsStudent = true,
            IsExternal = true,
        });
        await db.SaveChangesAsync();
        var service = new AdminDirectoryDataService(db);

        var exists = await service.DirectoryUserExistsAsync(" employee01 ", CancellationToken.None);

        exists.Should().Be(expected);
        db.AppUsers.Should().BeEmpty();
        db.EmployeeAccrualBalances.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing")]
    public async Task Directory_membership_rejects_blank_or_missing_identities(string iamId)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var service = new AdminDirectoryDataService(db);

        (await service.DirectoryUserExistsAsync(iamId, CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Admin_and_Cao_assignments_accept_an_employee_without_accruals_or_an_AppUser(
        bool? isFaculty)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person
        {
            IamId = "employee01",
            IsEmployee = true,
            IsFaculty = isFaculty,
        });
        var controller = await CreateControllerAsync(db);

        var adminResult = await controller.AddAdminAsync(new(" employee01 "), CancellationToken.None);
        var caoResult = await controller.AddCaoAsync(new(1, " employee01 "), CancellationToken.None);

        adminResult.Should().BeOfType<NoContentResult>();
        caoResult.Should().BeOfType<NoContentResult>();
        db.AppAdminAssignments.Should().ContainSingle().Which.IamId.Should().Be("employee01");
        db.ClusterCaoAssignments.Should().ContainSingle().Which.IamId.Should().Be("employee01");
        db.AppUsers.Should().ContainSingle().Which.IamId.Should().Be("adminactor");
        db.EmployeeAccrualBalances.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Admin_and_Cao_assignments_reject_nonemployees_even_with_an_AppUser(bool? isEmployee)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person
        {
            IamId = "nonemp",
            IsEmployee = isEmployee,
            IsFaculty = true,
            IsStudent = true,
            IsExternal = true,
        });
        db.AppUsers.Add(new AppUser { IamId = "nonemp", EntraObjectId = Guid.NewGuid() });
        var controller = await CreateControllerAsync(db);

        var adminResult = await controller.AddAdminAsync(new("nonemp"), CancellationToken.None);
        var caoResult = await controller.AddCaoAsync(new(1, "nonemp"), CancellationToken.None);

        AssertValidationProblem(adminResult, "Selected user must be a current directory user.");
        AssertValidationProblem(caoResult, "Selected user must be a current directory user.");
        db.AppAdminAssignments.Should().BeEmpty();
        db.ClusterCaoAssignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing")]
    public async Task Admin_and_Cao_assignments_reject_blank_or_missing_identities(string? iamId)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = await CreateControllerAsync(db);

        var adminResult = await controller.AddAdminAsync(new(iamId), CancellationToken.None);
        var caoResult = await controller.AddCaoAsync(new(1, iamId), CancellationToken.None);

        var expectedDetail = string.IsNullOrWhiteSpace(iamId)
            ? "IAM ID is required."
            : "Selected user must be a current directory user.";
        AssertValidationProblem(adminResult, expectedDetail);
        AssertValidationProblem(caoResult, expectedDetail);
        db.AppAdminAssignments.Should().BeEmpty();
        db.ClusterCaoAssignments.Should().BeEmpty();
    }

    private static void AssertValidationProblem(IActionResult result, string expectedDetail)
    {
        var problem = result.Should().BeAssignableTo<ObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Detail.Should().Be(expectedDetail);
    }

    private static async Task<AdminRolesController> CreateControllerAsync(AppDbContext db)
    {
        var actor = new AppUser { IamId = "adminactor", EntraObjectId = Guid.NewGuid() };
        db.AppUsers.Add(actor);
        db.Clusters.Add(new Cluster { Id = 1, ClusterName = "Test cluster" });
        await db.SaveChangesAsync();

        return new AdminRolesController(
            db,
            new AdminRolesService(new AdminDirectoryDataService(db)),
            new AdminDirectoryDataService(db),
            new UserService(NullLogger<UserService>.Instance, db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimConstants.ObjectId, actor.EntraObjectId.ToString())],
                        "Cookies")),
                },
            },
        };
    }
}
