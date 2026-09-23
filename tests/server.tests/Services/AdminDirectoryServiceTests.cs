using FluentAssertions;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class AdminDirectoryServiceTests
{
    [Fact]
    public async Task Cao_identity_loading_is_limited_to_requested_employees_regardless_of_faculty_status()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().AddRange(
            new Person { IamId = "staff00001", IsEmployee = true, IsFaculty = false, FullName = "Staff", Email = "staff@example.test" },
            new Person { IamId = "faculty001", IsEmployee = true, IsFaculty = true },
            new Person { IamId = "nullfac001", IsEmployee = true, IsFaculty = null },
            new Person { IamId = "other00001", IsEmployee = true, IsFaculty = true },
            new Person { IamId = "former0001", IsEmployee = false, IsFaculty = false },
            new Person { IamId = "unknown001", IsEmployee = null, IsFaculty = false });
        await db.SaveChangesAsync();

        var service = new AdminDirectoryDataService(db);
        var employees = await service.LoadCaoEmployeesAsync([" staff00001 ", "faculty001", "nullfac001", "former0001", "unknown001"], default);

        employees.Select(employee => employee.IamId).Should().BeEquivalentTo("staff00001", "faculty001", "nullfac001");
        employees.Single(employee => employee.IamId == "staff00001").DisplayName.Should().Be("Staff");
        employees.Single(employee => employee.IamId == "staff00001").Email.Should().Be("staff@example.test");
        (await service.LoadCaoEmployeesAsync([], default)).Should().BeEmpty();
        db.AppUsers.Should().BeEmpty();
        db.EmployeeAccrualBalances.Should().BeEmpty();
        (await service.LoadDirectoryDataAsync(default)).CurrentFaculty.Should().BeEmpty();
    }

    [Fact]
    public void Faculty_and_department_rosters_include_faculty_admins_and_Caos()
    {
        var data = CreateData();
        var faculty = AdminDirectoryService.BuildFacultyResponse(data);
        var departments = AdminDirectoryService.BuildDepartmentsResponse(data, CaoEmployees());

        faculty.FacultyUsers.Select(user => user.Id).Should().BeEquivalentTo("faculty", "admin", "cao");
        departments.FacultyUsers.Should().BeEquivalentTo(faculty.FacultyUsers);
        faculty.FacultyUsers.Single(user => user.Id == "admin").Role.Should().Be("admin");
        faculty.FacultyUsers.Single(user => user.Id == "cao").Role.Should().Be("cao");
        faculty.FacultyUsers.Should().OnlyContain(user => user.DepartmentId == "DEPT");
        departments.Clusters.Single(cluster => cluster.Id == "2").CaoUserId.Should().Be("staffcao");
        departments.CaoUsers.Single(user => user.Id == "staffcao").Name.Should().Be("Staff CAO");
        departments.CaoUsers.Single(user => user.Id == "staffcao").Email.Should().Be("staffcao@example.test");
    }

    [Fact]
    public void Cao_candidates_preserve_active_status_and_role_precedence_for_all_employees()
    {
        var response = AdminDirectoryService.BuildDepartmentsResponse(CreateData(), CaoEmployees());
        response.CaoUsers.Should().Contain(user => user.Id == "faculty" && user.Active && user.Designation == "faculty");
        response.CaoUsers.Should().Contain(user => user.Id == "unknown" && user.Active && user.Designation == "faculty");
        response.CaoUsers.Should().Contain(user => user.Id == "staff" && user.Active && user.Designation == "nfa");
        response.CaoUsers.Single(user => user.Id == "inactive").Active.Should().BeFalse();
        response.CaoUsers.Single(user => user.Id == "staffadmin").Designation.Should().Be("admin");
        response.CaoUsers.Single(user => user.Id == "staffchair").Designation.Should().Be("chair");
        response.CaoUsers.Single(user => user.Id == "staffcao").Designation.Should().Be("cao");
        response.CaoUsers.Single(user => user.Id == "unknown").Designation.Should().NotBe("nfa");
    }

    [Fact]
    public void Missing_cao_identity_retains_assignment_id_for_display_fallback()
    {
        var response = AdminDirectoryService.BuildDepartmentsResponse(CreateData(), []);
        response.Clusters.Single(cluster => cluster.Id == "2").CaoUserId.Should().Be("staffcao");
        response.CaoUsers.Should().BeEmpty();
    }

    private static AdminDirectoryData CreateData() => new(
        AppUsers: [new AppUser { IamId = "inactive", IsActive = false }],
        Clusters: [new Cluster { Id = 1, ClusterName = "Faculty cluster" }, new Cluster { Id = 2, ClusterName = "Staff cluster" }],
        CurrentCaoAssignmentsByCluster: new Dictionary<int, ClusterCaoAssignment>
        {
            [1] = new() { IamId = "cao", ClusterId = 1 },
            [2] = new() { IamId = "staffcao", ClusterId = 2 },
        },
        CurrentChairAssignmentsByDepartment: new Dictionary<string, DepartmentChairAssignment>
        {
            ["DEPT"] = new() { IamId = "staffchair", DepartmentCode = "DEPT" },
        },
        CurrentFaculty: new[] { "faculty", "admin", "cao" }.Select(iam => new CurrentEmployee
        {
            IamId = iam, DisplayName = $"Faculty {iam}", ResolvedReportingDepartmentCode = "DEPT", HasCurrentAccrualRecord = true,
        }).ToList(),
        CurrentOverridesById: new Dictionary<int, EmployeeReportingDepartmentOverride>(),
        Departments: [new Department { DepartmentCode = "DEPT", DepartmentName = "Department", ClusterId = 1 }],
        AdminIamIds: new HashSet<string> { "admin", "staffadmin" });

    private static IReadOnlyList<CaoDirectoryEmployee> CaoEmployees() => [
        new("staff", "Staff candidate", "staff@example.test", false),
        new("faculty", "Faculty candidate", null, true),
        new("inactive", "Inactive staff", null, false),
        new("staffadmin", "Staff admin", null, false),
        new("staffchair", "Staff chair", null, false),
        new("staffcao", "Staff CAO", "staffcao@example.test", false),
        new("cao", "Faculty CAO", null, true),
        new("unknown", "Unknown faculty flag", null, null),
    ];
}
