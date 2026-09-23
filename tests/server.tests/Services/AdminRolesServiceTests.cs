using FluentAssertions;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class AdminRolesServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 23);

    [Fact]
    public async Task Role_options_load_only_requested_employee_identities_without_accruals_or_AppUsers()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().AddRange(
            new Person { IamId = "staff00001", IsEmployee = true, IsFaculty = false, FullName = "Staff member", Email = "staff@example.test" },
            new Person { IamId = "fac0000001", IsEmployee = true, IsFaculty = true, FullName = "Faculty without accruals" },
            new Person { IamId = "other00001", IsEmployee = true, IsFaculty = false },
            new Person { IamId = "former0001", IsEmployee = false, IsFaculty = true },
            new Person { IamId = "unknown001", IsEmployee = null, IsFaculty = true });
        await db.SaveChangesAsync();

        var options = await new AdminDirectoryDataService(db).LoadRoleOptionsDataAsync(["staff00001", "fac0000001", "former0001", "unknown001"], CancellationToken.None);

        options.Employees.Select(employee => employee.IamId).Should().BeEquivalentTo("staff00001", "fac0000001");
        options.Employees.Single(employee => employee.IamId == "staff00001").DisplayName.Should().Be("Staff member");
        options.Employees.Single(employee => employee.IamId == "staff00001").Email.Should().Be("staff@example.test");
        options.CurrentFaculty.Should().BeEmpty();
        db.AppUsers.Should().BeEmpty();
        db.EmployeeAccrualBalances.Should().BeEmpty();
    }

    [Fact]
    public void Roles_use_employee_names_and_keep_nonaccrual_admins_and_Caos_active()
    {
        var options = CreateOptions();
        var assignments = new AdminRoleAssignmentsData(
            AdminAssignments: [new AppAdminAssignment { IamId = " staff " }, new AppAdminAssignment { IamId = "faculty" }],
            CaoAssignments: [new ClusterCaoAssignment { IamId = "staff", ClusterId = 1, EffectiveStartDate = Today }],
            ChairAssignments: [new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "DEPT", EffectiveStartDate = Today }]);

        var response = AdminRolesService.BuildRolesResponse(options, assignments, Today);

        response.Assignments.Should().OnlyContain(assignment => assignment.Active);
        response.Assignments.Where(assignment => assignment.IamId == "staff")
            .Should().OnlyContain(assignment => assignment.Name == "Staff member" && assignment.Email == "staff@example.test");
        var staff = response.Users.Single(user => user.IamId == "staff");
        staff.DepartmentOptions.Should().BeEmpty();
        staff.DepartmentId.Should().BeNull();
        var faculty = response.Users.Single(user => user.IamId == "faculty");
        faculty.Name.Should().Be("Faculty member");
        faculty.DepartmentOptions.Should().ContainSingle().Which.Id.Should().Be("DEPT");
    }

    [Fact]
    public void Cleanup_separates_employee_membership_from_chair_faculty_and_department_eligibility()
    {
        var options = CreateOptions();
        var validAdmin = new AppAdminAssignment { IamId = " STAFF " };
        var missingAdmin = new AppAdminAssignment { IamId = "missing" };
        var validCao = new ClusterCaoAssignment { IamId = "staff", ClusterId = 1 };
        var missingCao = new ClusterCaoAssignment { IamId = "missing", ClusterId = 1 };
        var inactiveTargetCao = new ClusterCaoAssignment { IamId = "staff", ClusterId = 2 };
        var validChair = new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "DEPT" };
        var nonFacultyChair = new DepartmentChairAssignment { IamId = "staff", DepartmentCode = "DEPT" };
        var movedChair = new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "OTHER" };
        var inactiveTargetChair = new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "INACTIVE" };

        var changes = AdminRolesService.GetInactiveRoleAssignmentChanges(
            [validAdmin, missingAdmin],
            [validCao, missingCao, inactiveTargetCao],
            [validChair, nonFacultyChair, movedChair, inactiveTargetChair],
            options.Employees,
            options.CurrentFaculty,
            options.Clusters,
            options.Departments);

        changes.AdminAssignmentsToDelete.Should().Equal(missingAdmin);
        changes.CaoAssignmentsToClose.Should().BeEquivalentTo([missingCao, inactiveTargetCao]);
        changes.ChairAssignmentsToClose.Should().BeEquivalentTo([nonFacultyChair, movedChair, inactiveTargetChair]);
    }

    [Theory]
    [InlineData(-1, null, false, true)]
    [InlineData(0, null, false, true)]
    [InlineData(1, null, false, false)]
    [InlineData(-1, 0, false, false)]
    [InlineData(-1, 1, false, true)]
    [InlineData(-1, null, true, false)]
    public void Role_responses_preserve_effective_dates_and_closed_assignments(
        int startOffset,
        int? endOffset,
        bool closed,
        bool expectedActive)
    {
        var start = Today.AddDays(startOffset);
        DateOnly? end = endOffset.HasValue ? Today.AddDays(endOffset.Value) : null;
        DateTime? closedUtc = closed ? Today.ToDateTime(TimeOnly.MinValue) : null;
        var assignments = new AdminRoleAssignmentsData(
            AdminAssignments: [],
            CaoAssignments: [new ClusterCaoAssignment { IamId = "staff", ClusterId = 1, EffectiveStartDate = start, EffectiveEndDateExclusive = end, ClosedUtc = closedUtc }],
            ChairAssignments: [new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "DEPT", EffectiveStartDate = start, EffectiveEndDateExclusive = end, ClosedUtc = closedUtc }]);

        var response = AdminRolesService.BuildRolesResponse(CreateOptions(), assignments, Today);

        response.Assignments.Should().HaveCount(2).And.OnlyContain(assignment => assignment.Active == expectedActive);
    }

    [Fact]
    public void Missing_employees_and_invalid_chair_targets_are_inactive_in_role_responses()
    {
        var assignments = new AdminRoleAssignmentsData(
            AdminAssignments: [new AppAdminAssignment { IamId = "missing" }],
            CaoAssignments: [
                new ClusterCaoAssignment { IamId = "missing", ClusterId = 1, EffectiveStartDate = Today },
                new ClusterCaoAssignment { IamId = "staff", ClusterId = 2, EffectiveStartDate = Today }],
            ChairAssignments: [
                new DepartmentChairAssignment { IamId = "staff", DepartmentCode = "DEPT", EffectiveStartDate = Today },
                new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "OTHER", EffectiveStartDate = Today },
                new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "INACTIVE", EffectiveStartDate = Today }]);

        var response = AdminRolesService.BuildRolesResponse(CreateOptions(), assignments, Today);

        response.Assignments.Should().OnlyContain(assignment => !assignment.Active);
        response.Assignments.Where(assignment => assignment.IamId == "missing")
            .Should().OnlyContain(assignment => assignment.Name == "missing");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(5, 123)]
    [InlineData(-1, 123)]
    public void Closing_assignments_preserves_past_end_dates_and_records_the_actor(int? endOffset, int? actorId)
    {
        DateOnly? end = endOffset.HasValue ? Today.AddDays(endOffset.Value) : null;
        var expectedEnd = endOffset < 0 ? end : Today;
        var now = Today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var cao = new ClusterCaoAssignment { IamId = "staff", EffectiveEndDateExclusive = end, ClosedByAppUserId = 99 };
        var chair = new DepartmentChairAssignment { IamId = "faculty", DepartmentCode = "DEPT", EffectiveEndDateExclusive = end, ClosedByAppUserId = 99 };

        AdminRolesService.CloseClusterCaoAssignment(cao, actorId, now, Today);
        AdminRolesService.CloseDepartmentChairAssignment(chair, actorId, now, Today);

        cao.EffectiveEndDateExclusive.Should().Be(expectedEnd);
        chair.EffectiveEndDateExclusive.Should().Be(expectedEnd);
        cao.ClosedUtc.Should().Be(now);
        chair.ClosedUtc.Should().Be(now);
        cao.ClosedByAppUserId.Should().Be(actorId ?? 99);
        chair.ClosedByAppUserId.Should().Be(actorId ?? 99);
    }

    private static AdminRoleOptionsData CreateOptions() => new(
        Clusters: [new Cluster { Id = 1, ClusterName = "Cluster" }, new Cluster { Id = 2, ClusterName = "Inactive", IsActive = false }],
        Employees: [new DirectoryEmployee("staff", "Staff member", "staff@example.test"), new DirectoryEmployee("faculty", "Faculty member", "faculty@example.test")],
        CurrentFaculty: [new CurrentEmployee { IamId = "faculty", HasCurrentAccrualRecord = true, ResolvedReportingDepartmentCode = "DEPT", ResolvedReportingDepartmentName = "Department" }],
        Departments: [
            new Department { DepartmentCode = "DEPT", DepartmentName = "Department" },
            new Department { DepartmentCode = "OTHER", DepartmentName = "Other department" },
            new Department { DepartmentCode = "INACTIVE", DepartmentName = "Inactive department", IsActive = false }]);
}
