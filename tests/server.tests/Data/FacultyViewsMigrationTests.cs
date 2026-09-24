using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class FacultyViewsMigrationTests
{
    private const string PreviousMigration = "20260828170808_AddEmployeeAccrualImportSupport";
    private const string FacultyMigration = "20260923231654_ReplaceCurrentViewsWithFacultyViews";

    [SandboxFact]
    public async Task Migration_preserves_faculty_query_behavior_across_upgrade_rollback_and_reupgrade()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server=sql,1433;Database=FacultyViewsTest_{Guid.NewGuid():N};User ID=sa;Password=LocalDev123!;Encrypt=False;Pooling=False")
            .Options;
        await using var db = new AppDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await SeedAsync(db);
            var previousFaculty = await SnapshotAsync(db, "SELECT (SELECT * FROM dbo.vw_CurrentEmployee ORDER BY IamId FOR JSON PATH) AS [Value]");
            var previousBalances = await SnapshotAsync(db, "SELECT (SELECT * FROM dbo.vw_CurrentAccrualBalance ORDER BY IamId, LeaveTypeNumber FOR JSON PATH) AS [Value]");

            await migrator.MigrateAsync(FacultyMigration);
            await AssertFacultyViewsAsync(db);
            await migrator.MigrateAsync(PreviousMigration);
            (await ViewNamesAsync(db)).Should().BeEquivalentTo("vw_CurrentEmployee", "vw_CurrentAccrualBalance");
            (await SnapshotAsync(db, "SELECT (SELECT * FROM dbo.vw_CurrentEmployee ORDER BY IamId FOR JSON PATH) AS [Value]")).Should().Be(previousFaculty);
            (await SnapshotAsync(db, "SELECT (SELECT * FROM dbo.vw_CurrentAccrualBalance ORDER BY IamId, LeaveTypeNumber FOR JSON PATH) AS [Value]")).Should().Be(previousBalances);
            await migrator.MigrateAsync(FacultyMigration);
            await AssertFacultyViewsAsync(db);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var index = 0;
        foreach (var employee in new bool?[] { true, false, null })
        foreach (var faculty in new bool?[] { true, false, null })
        {
            var id = $"{++index:D8}";
            db.Set<Person>().Add(new Person { IamId = id, EmployeeId = id, IsEmployee = employee, IsFaculty = faculty });
            db.Set<EmployeeAccrualBalance>().Add(Balance(id, "1", new DateOnly(2026, 1, 1), 10));
        }
        db.Set<Person>().Add(new Person { IamId = "noaccrual", EmployeeId = "99999999", IsEmployee = true, IsFaculty = true });
        db.Set<Person>().Add(new Person { IamId = "ranked", EmployeeId = "88888888", IsEmployee = true, IsFaculty = true, FullName = "People name", Email = "people@example.test" });
        var date = new DateOnly(2026, 2, 1);
        db.Set<EmployeeAccrualBalance>().AddRange(
            Balance("88888888", "1", date.AddDays(-1), 999, leaveType: 2),
            Balance("88888888", "1", date, 30, job: " "),
            Balance("88888888", "2", date, 20, employeeClass: " "),
            Balance("88888888", "3", date, 10),
            Balance("88888888", "4", date, 10),
            Balance("88888888", "3", date, 15, leaveType: 3),
            Balance("88888888", "4", date, 15, leaveType: 3));
        await db.SaveChangesAsync();
    }

    private static async Task AssertFacultyViewsAsync(AppDbContext db)
    {
        (await ViewNamesAsync(db)).Should().BeEquivalentTo("vw_CurrentFacultyWithAccrual", "vw_CurrentFacultyAccrualBalance");
        (await db.CurrentFacultyWithAccrual.Select(row => row.IamId.Trim()).ToListAsync())
            .Should().BeEquivalentTo("00000001", "ranked");
        (await db.CurrentFacultyAccrualBalances.Select(row => row.IamId.Trim()).Distinct().ToListAsync())
            .Should().BeEquivalentTo("00000001", "ranked");
        var earlier = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "00000001");
        earlier.LatestAsOfDate.Should().Be(new DateOnly(2026, 1, 1));
        earlier.DisplayName.Should().Be("Accrual name");
        earlier.Email.Should().Be("accrual@example.test");
        var ranked = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "ranked");
        ranked.LatestAsOfDate.Should().Be(new DateOnly(2026, 2, 1));
        ranked.IsFaculty.Should().BeTrue();
        ranked.DisplayName.Should().Be("People name");
        ranked.Email.Should().Be("people@example.test");
        ranked.SourceDepartmentCode.Should().Be("L5-3");
        ranked.SourceDepartmentName.Should().Be("Level five 3");
        ranked.ResolvedReportingDepartmentCode.Should().Be("L5-3");
        ranked.HasReportingDepartmentOverride.Should().BeFalse();
        var balances = await db.CurrentFacultyAccrualBalances.Where(row => row.IamId == "ranked").OrderBy(row => row.LeaveTypeNumber).ToListAsync();
        balances.Select(row => row.LeaveTypeNumber).Should().Equal(1, 3);
        balances.Should().OnlyContain(row => row.LatestAsOfDate == new DateOnly(2026, 2, 1));
        balances[0].CalculatedBal.Should().Be(30);
        balances[0].PositionRowCount.Should().Be(4);
        balances[0].MinCalculatedBal.Should().Be(10);
        balances[0].MaxCalculatedBal.Should().Be(30);
        balances[0].HasDivergentPositionBalances.Should().BeTrue();
        balances[1].CalculatedBal.Should().Be(15);
        balances[1].PositionRowCount.Should().Be(2);
        balances[1].HasDivergentPositionBalances.Should().BeFalse();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.EmployeeAccrualBalances.Where(row => row.EmployeeId == "88888888" && row.PositionNumber == "3")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Level5Dept, " "));
        ranked = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "ranked");
        ranked.SourceDepartmentCode.Should().Be("L4");
        ranked.SourceDepartmentName.Should().Be("Level four");
        await db.EmployeeAccrualBalances.Where(row => row.EmployeeId == "88888888" && row.PositionNumber == "3")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Level4Dept, " "));
        ranked = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "ranked");
        ranked.SourceDepartmentCode.Should().Be("L3");
        ranked.SourceDepartmentName.Should().Be("Level three");
        await db.EmployeeAccrualBalances.Where(row => row.EmployeeId == "88888888" && row.PositionNumber == "3")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Level3Dept, " "));
        ranked = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "ranked");
        ranked.SourceDepartmentCode.Should().BeNull();
        ranked.ResolvedReportingDepartmentCode.Should().BeNull();

        await AssertOverridesAsync(db);
        await transaction.RollbackAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task AssertOverridesAsync(AppDbContext db)
    {
        var utc = await db.Database.SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS [Value]").SingleAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")));
        var actor = new AppUser { IamId = "actor", EntraObjectId = Guid.NewGuid() };
        db.AppUsers.Add(actor);
        db.Departments.Add(new Department { DepartmentCode = "override", DepartmentName = "Override department", IsActive = false });
        await db.SaveChangesAsync();
        var records = new[]
        {
            new EmployeeReportingDepartmentOverride { IamId = "00000001", DepartmentCode = "override", CreatedByAppUserId = actor.Id, EffectiveStartDate = today.AddDays(-2) },
            new EmployeeReportingDepartmentOverride { IamId = "00000001", DepartmentCode = "override", CreatedByAppUserId = actor.Id, EffectiveStartDate = today },
            new EmployeeReportingDepartmentOverride { IamId = "00000001", DepartmentCode = "override", CreatedByAppUserId = actor.Id, EffectiveStartDate = today },
            new EmployeeReportingDepartmentOverride { IamId = "00000001", DepartmentCode = "override", CreatedByAppUserId = actor.Id, EffectiveStartDate = today.AddDays(1) },
            new EmployeeReportingDepartmentOverride { IamId = "00000001", DepartmentCode = "override", CreatedByAppUserId = actor.Id, EffectiveStartDate = today, EffectiveEndDateExclusive = today }
        };
        foreach (var record in records)
        {
            db.EmployeeReportingDepartmentOverrides.Add(record);
            await db.SaveChangesAsync();
        }
        var faculty = await db.CurrentFacultyWithAccrual.SingleAsync(row => row.IamId == "00000001");
        faculty.ReportingDepartmentOverrideId.Should().Be(records[2].Id);
        faculty.HasReportingDepartmentOverride.Should().BeTrue();
        faculty.SourceDepartmentCode.Should().Be("L5-1");
        faculty.ResolvedReportingDepartmentCode.Should().Be("override");
        faculty.ResolvedReportingDepartmentName.Should().Be("Override department");
    }

    private static Task<List<string>> ViewNamesAsync(AppDbContext db) => db.Database
        .SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.views WHERE name LIKE 'vw_Current%'").ToListAsync();

    private static Task<string> SnapshotAsync(AppDbContext db, string query) => db.Database
        .SqlQueryRaw<string>(query).SingleAsync();

    private static EmployeeAccrualBalance Balance(string id, string position, DateOnly date, decimal amount, string job = "A", string employeeClass = "A", int leaveType = 1) => new()
    {
        EmployeeId = id, PositionNumber = position, AsOfDate = date, LeaveTypeNumber = leaveType,
        EmployeeName = "Accrual name", EmployeeEmail = "accrual@example.test", UnionCode = "", UnionDescription = "",
        EmployeeClassCode = employeeClass, EmployeeClassDescription = "", JobCode = job, JobCodeDescription = "",
        HrStatus = "A", EmployeeStatus = "A", EmployeeStatusDescription = "", EmployeeType = "", EmployeeTypeDescription = "",
        TypeLabel = "Vacation", CalculatedBal = amount, ApproachingMax = "N",
        Level1Dept = "", Level1DeptDesc = "", Level2Dept = "", Level2DeptDesc = "",
        Level3Dept = "L3", Level3DeptDesc = "Level three", Level4Dept = "L4", Level4DeptDesc = "Level four",
        Level5Dept = $"L5-{position}", Level5DeptDesc = $"Level five {position}", LastUpdated = DateTime.UtcNow
    };

    public sealed class SandboxFactAttribute : FactAttribute
    {
        public SandboxFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("LEAVES_SANDBOX_TESTS") != "1")
                Skip = "Run inside dev/sandbox with LEAVES_SANDBOX_TESTS=1; see docs/FACULTY-VIEWS-IMPLEMENTATION.md.";
        }
    }
}
