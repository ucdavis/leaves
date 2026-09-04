using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Web;
using Server.Controllers;
using Server.Core.Data;
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

    [Fact]
    public async Task GetRolesForUser_returns_only_current_directory_and_assignment_roles()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddRoleAssignmentScope(db);
        var activeUser = AddDirectoryUser(db, "activeuser");
        AddDirectoryUser(db, "expireduser");
        AddDirectoryUser(db, "futureuser");
        AddDirectoryUser(db, "closeduser");
        await db.SaveChangesAsync();

        db.AppAdminAssignments.Add(new AppAdminAssignment
        {
            CreatedByAppUserId = activeUser.Id,
            IamId = "activeuser",
        });
        db.DepartmentChairAssignments.AddRange(
            CreateChairAssignment("activeuser", activeUser.Id, today.AddDays(-1), null, null),
            CreateChairAssignment("expireduser", activeUser.Id, today.AddDays(-2), today, null),
            CreateChairAssignment("futureuser", activeUser.Id, today.AddDays(1), null, null),
            CreateChairAssignment("closeduser", activeUser.Id, today.AddDays(-1), null, DateTime.UtcNow));
        db.ClusterCaoAssignments.AddRange(
            CreateCaoAssignment("activeuser", activeUser.Id, today.AddDays(-1), null, null),
            CreateCaoAssignment("expireduser", activeUser.Id, today.AddDays(-2), today, null),
            CreateCaoAssignment("futureuser", activeUser.Id, today.AddDays(1), null, null),
            CreateCaoAssignment("closeduser", activeUser.Id, today.AddDays(-1), null, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = new UserService(NullLogger<UserService>.Instance, db);

        (await service.GetRolesForUser(activeUser.EntraObjectId.ToString()))
            .Should().BeEquivalentTo(["Admin", "Faculty", "Chair", "CAO"]);
        (await service.GetRolesForUser(GetUserId(db, "expireduser")))
            .Should().BeEquivalentTo(["Faculty"]);
        (await service.GetRolesForUser(GetUserId(db, "futureuser")))
            .Should().BeEquivalentTo(["Faculty"]);
        (await service.GetRolesForUser(GetUserId(db, "closeduser")))
            .Should().BeEquivalentTo(["Faculty"]);
    }

    [Fact]
    public async Task UpdateUserPrincipalIfNeeded_refreshes_the_roles_returned_by_user_me()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddRoleAssignmentScope(db);
        var user = AddDirectoryUser(db, "chairuser");
        await db.SaveChangesAsync();
        db.DepartmentChairAssignments.Add(
            CreateChairAssignment("chairuser", user.Id, today.AddDays(-1), null, null));
        await db.SaveChangesAsync();

        var principal = CreatePrincipal(user.EntraObjectId, "chair@example.com", "chairuser");
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, "CAO"));
        var service = new UserService(NullLogger<UserService>.Instance, db);

        var refreshed = await service.UpdateUserPrincipalIfNeeded(principal);

        refreshed.Should().NotBeNull();
        refreshed!.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Should().BeEquivalentTo(["Faculty", "Chair"]);

        var controller = new UserController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = refreshed },
            },
        };

        var response = await controller.Me() as OkObjectResult;

        response.Should().NotBeNull();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response!.Value));
        document.RootElement.GetProperty("Roles")
            .EnumerateArray()
            .Select(role => role.GetString())
            .Should().BeEquivalentTo(["Faculty", "Chair"]);
    }

    [Fact]
    public async Task GetRolesForUser_omits_faculty_and_chair_without_a_current_accrual_balance()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddRoleAssignmentScope(db);
        var user = AddDirectoryUser(db, "noaccrual", hasAccrualBalance: false);
        await db.SaveChangesAsync();
        db.DepartmentChairAssignments.Add(
            CreateChairAssignment("noaccrual", user.Id, today.AddDays(-1), null, null));
        await db.SaveChangesAsync();

        var service = new UserService(NullLogger<UserService>.Instance, db);

        (await service.GetRolesForUser(user.EntraObjectId.ToString()))
            .Should().BeEmpty();
    }

    private static AppUser AddDirectoryUser(
        AppDbContext db,
        string iamId,
        bool hasAccrualBalance = true)
    {
        var user = new AppUser
        {
            DisplayName = iamId,
            EmployeeId = $"employee-{iamId}",
            EntraObjectId = Guid.NewGuid(),
            FirstLoginUtc = DateTime.UtcNow,
            IamId = iamId,
            IsActive = true,
        };
        db.AppUsers.Add(user);
        db.Set<Person>().Add(new Person
        {
            EmployeeId = user.EmployeeId,
            IamId = iamId,
            IsEmployee = true,
            IsFaculty = true,
        });
        if (hasAccrualBalance)
        {
            db.Set<EmployeeAccrualBalance>().Add(new EmployeeAccrualBalance
            {
                AccrualHours = 8m,
                AccrualLimit = 240m,
                AccrualPercentage = 50m,
                ApproachingMax = "N",
                AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CalculatedBal = 80m,
                EmployeeClassCode = "FAC",
                EmployeeClassDescription = "Faculty",
                EmployeeId = user.EmployeeId,
                EmployeeName = iamId,
                EmployeeStatus = "Active",
                EmployeeStatusDescription = "Active",
                EmployeeType = "Faculty",
                EmployeeTypeDescription = "Faculty",
                ExceptionalMaxVacationOnly = 0,
                HoursOverUnderPolicyMax = 0m,
                HoursTaken = 0m,
                HourlyRateFTE = 1m,
                HrStatus = "Active",
                JobCode = "001700",
                JobCodeDescription = "Professor",
                LastUpdated = DateTime.UtcNow,
                LeaveTypeNumber = 10,
                Level1Dept = "D001",
                Level1DeptDesc = "Test Department",
                Level2Dept = "D001",
                Level2DeptDesc = "Test Department",
                Level3Dept = "D001",
                Level3DeptDesc = "Test Department",
                Level4Dept = "D001",
                Level4DeptDesc = "Test Department",
                Level5Dept = "D001",
                Level5DeptDesc = "Test Department",
                PositionNumber = "P0000001",
                PrevBal = 80m,
                TypeLabel = "Vacation",
                UnionCode = "FAC",
                UnionDescription = "Faculty",
            });
        }
        return user;
    }

    private static void AddRoleAssignmentScope(AppDbContext db)
    {
        db.Clusters.Add(new Cluster
        {
            ClusterName = "Test Cluster",
            Id = 1,
            IsActive = true,
        });
        db.Departments.Add(new Department
        {
            ClusterId = 1,
            CreatedUtc = DateTime.UtcNow,
            DepartmentCode = "D001",
            DepartmentName = "Test Department",
            IsActive = true,
            UpdatedUtc = DateTime.UtcNow,
            WorkflowMode = WorkflowMode.ApprovalRequired,
        });
    }

    private static DepartmentChairAssignment CreateChairAssignment(
        string iamId,
        int createdByAppUserId,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime? closedUtc) =>
        new()
        {
            CreatedByAppUserId = createdByAppUserId,
            CreatedUtc = DateTime.UtcNow,
            ClosedUtc = closedUtc,
            DepartmentCode = "D001",
            EffectiveEndDateExclusive = endDate,
            EffectiveStartDate = startDate,
            IamId = iamId,
        };

    private static ClusterCaoAssignment CreateCaoAssignment(
        string iamId,
        int createdByAppUserId,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime? closedUtc) =>
        new()
        {
            ClusterId = 1,
            CreatedByAppUserId = createdByAppUserId,
            CreatedUtc = DateTime.UtcNow,
            ClosedUtc = closedUtc,
            EffectiveEndDateExclusive = endDate,
            EffectiveStartDate = startDate,
            IamId = iamId,
        };

    private static string GetUserId(AppDbContext db, string iamId) =>
        db.AppUsers.Single(user => user.IamId == iamId).EntraObjectId.ToString();

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
