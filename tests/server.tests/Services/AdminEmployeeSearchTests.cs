using FluentAssertions;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class AdminEmployeeSearchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" a ")]
    public async Task Search_requires_two_characters(string? query)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person { IamId = "staff00001", FullName = "Staff", IsEmployee = true });
        await db.SaveChangesAsync();
        var service = new AdminDirectoryDataService(db);

        (await service.SearchEmployeeIdsAsync(query, true, default)).Should().BeEmpty();
        (await service.SearchEmployeeIdsAsync(new string('a', 129), true, default)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(" Needle ")]
    [InlineData("staff@example.test")]
    [InlineData("staff00001")]
    public async Task Search_matches_name_email_or_IamId_without_accruals_or_AppUser(string query)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        db.Set<Person>().Add(new Person { IamId = "staff00001", FullName = "Needle Faculty", Email = "staff@example.test", IsEmployee = true, IsFaculty = true });
        await db.SaveChangesAsync();
        var service = new AdminDirectoryDataService(db);

        (await service.SearchEmployeeIdsAsync(query, true, default)).Should().Equal("staff00001");
        db.AppUsers.Should().BeEmpty();
        db.EmployeeAccrualBalances.Should().BeEmpty();
    }

    [Fact]
    public async Task Cao_search_preserves_employee_active_and_role_rules_including_assignment_dates()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ids = new[] { "staff", "faculty", "nullfac", "gone", "unknown", "inactive", "admin", "cao", "chair", "future", "expired", "closed" };
        foreach (var id in ids)
        {
            db.Set<Person>().Add(new Person {
                IamId = id.PadRight(10, '0'), FullName = $"Needle {id}",
                IsEmployee = id == "gone" ? false : id == "unknown" ? null : true,
                IsFaculty = id == "nullfac" ? null : id == "faculty",
            });
        }
        db.Clusters.AddRange(Enumerable.Range(1, 4).Select(id => new Cluster { Id = id, ClusterName = $"Cluster {id}" }));
        db.Departments.Add(new Department { DepartmentCode = "DEPT", DepartmentName = "Department" });
        db.AppUsers.Add(new AppUser { IamId = "inactive00", IsActive = false });
        db.AppAdminAssignments.Add(new AppAdminAssignment { IamId = "admin00000" });
        db.ClusterCaoAssignments.AddRange(
            new ClusterCaoAssignment { IamId = "cao0000000", ClusterId = 1, EffectiveStartDate = today },
            new ClusterCaoAssignment { IamId = "future0000", ClusterId = 2, EffectiveStartDate = today.AddDays(1) },
            new ClusterCaoAssignment { IamId = "expired000", ClusterId = 3, EffectiveStartDate = today.AddDays(-1), EffectiveEndDateExclusive = today },
            new ClusterCaoAssignment { IamId = "closed0000", ClusterId = 4, EffectiveStartDate = today, ClosedUtc = DateTime.UtcNow });
        db.DepartmentChairAssignments.Add(new DepartmentChairAssignment { IamId = "chair00000", DepartmentCode = "DEPT", EffectiveStartDate = today });
        await db.SaveChangesAsync();
        var service = new AdminDirectoryDataService(db);

        (await service.SearchEmployeeIdsAsync("Needle", true, default)).Should().BeEquivalentTo(
            "staff00000", "faculty000", "nullfac000", "future0000", "expired000", "closed0000");
        // Admin search preserves its broader selection policy, including inactive AppUsers and other roles.
        (await service.SearchEmployeeIdsAsync("Needle", false, default)).Should().BeEquivalentTo(
            "staff00000", "faculty000", "nullfac000", "future0000", "expired000", "closed0000", "inactive00", "cao0000000", "chair00000");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Search_filters_before_capping_at_twenty_and_orders_deterministically(bool forCao)
    {
        using var db = TestDbContextFactory.CreateInMemory();
        for (var index = 59; index >= 0; index--)
        {
            var iamId = $"iam{index:0000000}";
            db.Set<Person>().Add(new Person { IamId = iamId, FullName = $"Needle {index:000}", IsEmployee = true });
            if (index < 30) db.AppAdminAssignments.Add(new AppAdminAssignment { IamId = iamId });
        }
        await db.SaveChangesAsync();
        var service = new AdminDirectoryDataService(db);

        var ids = await service.SearchEmployeeIdsAsync("Needle", forCao, default);

        ids.Should().Equal(Enumerable.Range(30, 20).Select(index => $"iam{index:0000000}"));
    }
}
