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
    public async Task Faculty_with_current_overrides_are_loaded_from_people_not_accruals()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Set<Person>().AddRange(
            new Person { IamId = "included", IsEmployee = true, IsFaculty = true, IsActiveInIam = false, FullName = "Included" },
            new Person { IamId = "activeiam", IsEmployee = true, IsFaculty = true, IsActiveInIam = true },
            new Person { IamId = "staff", IsEmployee = true, IsFaculty = false, IsActiveInIam = false },
            new Person { IamId = "notemployee", IsEmployee = false, IsFaculty = true, IsActiveInIam = false },
            new Person { IamId = "expired", IsEmployee = true, IsFaculty = true, IsActiveInIam = false });
        db.EmployeeReportingDepartmentOverrides.AddRange(
            new EmployeeReportingDepartmentOverride { Id = 1, IamId = "included", DepartmentCode = "DEPT", EffectiveStartDate = today.AddDays(-2), CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 2, IamId = "included", DepartmentCode = "NEWDEPT", EffectiveStartDate = today.AddDays(-1), CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 3, IamId = "activeiam", DepartmentCode = "DEPT", EffectiveStartDate = today, CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 4, IamId = "staff", DepartmentCode = "DEPT", EffectiveStartDate = today, CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 5, IamId = "notemployee", DepartmentCode = "DEPT", EffectiveStartDate = today, CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 6, IamId = "expired", DepartmentCode = "DEPT", EffectiveStartDate = today.AddDays(-2), EffectiveEndDateExclusive = today, CreatedByAppUserId = 1 });
        await db.SaveChangesAsync();

        var results = await new AdminDirectoryDataService(db)
            .LoadFacultyWithOverridesAsync(default);

        results.Select(result => result.IamId).Should().BeEquivalentTo("included", "activeiam");
        results.Single(result => result.IamId == "included").DepartmentCode.Should().Be("NEWDEPT");
        results.Single(result => result.IamId == "included").IsActiveInIam.Should().BeFalse();
        results.Single(result => result.IamId == "activeiam").IsActiveInIam.Should().BeTrue();
        db.EmployeeAccrualBalances.Should().BeEmpty();
    }

    [Fact]
    public async Task Faculty_with_overrides_preserve_current_chair_role()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Set<Person>().Add(new Person
        {
            IamId = "testchair",
            IsEmployee = true,
            IsFaculty = true,
            IsActiveInIam = false,
        });
        db.Departments.Add(new Department { DepartmentCode = "DEPT", DepartmentName = "Department" });
        db.EmployeeReportingDepartmentOverrides.Add(new EmployeeReportingDepartmentOverride
        {
            IamId = "testchair",
            DepartmentCode = "DEPT",
            EffectiveStartDate = today,
            CreatedByAppUserId = 1,
        });
        db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            DepartmentCode = "DEPT",
            IamId = "testchair",
            EffectiveStartDate = today,
            CreatedByAppUserId = 1,
        });
        await db.SaveChangesAsync();

        var results = await new AdminDirectoryService(new AdminDirectoryDataService(db))
            .GetFacultyWithOverridesAsync(default);

        results.Should().ContainSingle();
        results.Single().Role.Should().Be("chair");
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
        faculty.FacultyUsers.Single(user => user.Id == "faculty").IsActiveInIam.Should().BeFalse();
        faculty.FacultyUsers.Where(user => user.Id != "faculty").Should().OnlyContain(user => user.IsActiveInIam);
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
        CurrentFaculty: new[] { "faculty", "admin", "cao" }.Select(iam => new CurrentFacultyWithAccrual
        {
            IamId = iam,
            DisplayName = $"Faculty {iam}",
            IsActiveInIam = iam != "faculty",
            ResolvedReportingDepartmentCode = "DEPT",
            IsFaculty = true,
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
