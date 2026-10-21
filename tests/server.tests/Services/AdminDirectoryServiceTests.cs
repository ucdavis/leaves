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
    public async Task Current_overrides_are_loaded_directly_without_faculty_or_employee_filters()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Pacific Standard Time"));
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
            new EmployeeReportingDepartmentOverride { Id = 6, IamId = "expired", DepartmentCode = "DEPT", EffectiveStartDate = today.AddDays(-2), EffectiveEndDateExclusive = today, CreatedByAppUserId = 1 },
            new EmployeeReportingDepartmentOverride { Id = 7, IamId = "former", DepartmentCode = "DEPT", EffectiveStartDate = today, CreatedByAppUserId = 1 });
        await db.SaveChangesAsync();

        var results = await new AdminDirectoryDataService(db)
            .LoadFacultyWithOverridesAsync(default);

        results.Select(result => result.IamId).Should().BeEquivalentTo(
            "included", "activeiam", "staff", "notemployee", "former");
        results.Single(result => result.IamId == "included").DepartmentCode.Should().Be("NEWDEPT");
        results.Single(result => result.IamId == "included").IsActiveInIam.Should().BeFalse();
        results.Single(result => result.IamId == "activeiam").IsActiveInIam.Should().BeTrue();
        results.Single(result => result.IamId == "former").FullName.Should().BeNull();
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
    public void Faculty_roster_includes_faculty_admins_and_Caos()
    {
        var data = CreateData();
        var faculty = AdminDirectoryService.BuildFacultyResponse(data);

        faculty.FacultyUsers.Select(user => user.Id).Should().BeEquivalentTo("faculty", "admin", "cao");
        faculty.FacultyUsers.Single(user => user.Id == "admin").Role.Should().Be("admin");
        faculty.FacultyUsers.Single(user => user.Id == "cao").Role.Should().Be("cao");
        faculty.FacultyUsers.Single(user => user.Id == "faculty").IsActiveInIam.Should().BeFalse();
        faculty.FacultyUsers.Where(user => user.Id != "faculty").Should().OnlyContain(user => user.IsActiveInIam);
        faculty.FacultyUsers.Should().OnlyContain(user => user.DepartmentId == "DEPT");
    }

    [Fact]
    public void Department_overview_uses_counts_and_assignee_names_without_user_roles()
    {
        var response = AdminDirectoryService.BuildDepartmentsResponse(CreateOverviewData());
        response.Departments.Single().FacultyCount.Should().Be(3);
        response.Departments.Single().HasFaculty.Should().BeTrue();
        response.Departments.Single().ChairName.Should().Be("Staff chair");
        response.Clusters.Single(cluster => cluster.Id == "2").CaoName.Should().Be("Staff CAO");
    }

    [Fact]
    public void Departments_without_faculty_are_marked_and_missing_assignee_uses_id()
    {
        var data = CreateOverviewData() with
        {
            FacultyCountsByDepartment = new Dictionary<string, int>(),
            AssignedPeopleByIamId = new Dictionary<string, CaoDirectoryEmployee>(),
        };
        var response = AdminDirectoryService.BuildDepartmentsResponse(data);
        response.Departments.Single().FacultyCount.Should().Be(0);
        response.Departments.Single().HasFaculty.Should().BeFalse();
        response.Clusters.Single(cluster => cluster.Id == "2").CaoUserId.Should().Be("staffcao");
        response.Clusters.Single(cluster => cluster.Id == "2").CaoName.Should().Be("staffcao");
    }

    private static AdminDepartmentOverviewData CreateOverviewData()
    {
        var data = CreateData();
        return new AdminDepartmentOverviewData(
            data.Clusters, data.Departments, data.CurrentChairAssignmentsByDepartment,
            data.CurrentCaoAssignmentsByCluster,
            new Dictionary<string, int> { ["DEPT"] = 3 },
            CaoEmployees().ToDictionary(person => person.IamId));
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
