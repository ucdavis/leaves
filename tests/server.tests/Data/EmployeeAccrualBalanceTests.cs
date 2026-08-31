using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class EmployeeAccrualBalanceTests
{
    [Fact]
    public void ModelMatchesSourceTableContract()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMetadataOnly;User ID=sa;Password=not-used")
            .Options;
        using var db = new AppDbContext(options);

        var entity = db.Model.FindEntityType(typeof(EmployeeAccrualBalance));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("EmployeeAccrualBalances");
        entity.GetSchema().Should().Be("dbo");
        entity.FindPrimaryKey()!.GetName().Should().Be("PK_EmployeeAccrualBalances");
        entity.FindPrimaryKey()!.Properties.Select(property => property.Name).Should().Equal(
            nameof(EmployeeAccrualBalance.EmployeeId),
            nameof(EmployeeAccrualBalance.AsOfDate),
            nameof(EmployeeAccrualBalance.PositionNumber),
            nameof(EmployeeAccrualBalance.LeaveTypeNumber));

        entity.GetIndexes().Select(index => index.GetDatabaseName()).Should().Contain([
            "IX_EmployeeAccrualBalances_EmployeeId",
            "IX_EmployeeAccrualBalances_AsOf_Employee_LeaveType",
        ]);
        entity.FindProperty(nameof(EmployeeAccrualBalance.HourlyRateFTE))!.GetColumnType().Should().Be("decimal(12,4)");
        entity.FindProperty(nameof(EmployeeAccrualBalance.AccrualPercentage))!.GetColumnType().Should().Be("decimal(7,2)");
        entity.FindProperty(nameof(EmployeeAccrualBalance.LastUpdated))!.GetColumnType().Should().Be("datetime2(3)");
        entity.GetProperties()
            .Where(property => property.IsNullable)
            .Select(property => property.Name)
            .Should().BeEquivalentTo([
                nameof(EmployeeAccrualBalance.EmployeeEmail),
                nameof(EmployeeAccrualBalance.ReportsToPositionNumber),
                nameof(EmployeeAccrualBalance.ReportsToEmployeeId),
                nameof(EmployeeAccrualBalance.ReportsToEmployeeName),
                nameof(EmployeeAccrualBalance.LoadDate),
            ]);
    }

    [Fact]
    public async Task SeededAccrualBalancesQueryIsNoTracking()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        await SeedAccrualBalancesAsync(db);

        db.ChangeTracker.Clear();
        var balances = await db.EmployeeAccrualBalances.ToListAsync();

        balances.Should().HaveCount(7);
        db.ChangeTracker.Entries<EmployeeAccrualBalance>().Should().BeEmpty();

        var localFacultyVacation = balances.Single(balance =>
            balance.EmployeeId == DevelopmentSeedData.LocalFacultyEmployeeId &&
            balance.AsOfDate == new DateOnly(2026, 7, 12) &&
            balance.LeaveTypeNumber == 10);

        localFacultyVacation.ApproachingMax.Should().Be("N");
        localFacultyVacation.HoursOverUnderPolicyMax.Should().Be(144.00m);
        localFacultyVacation.AccrualPercentage.Should().Be(40.00m);
        localFacultyVacation.Level5Dept.Should().Be("030045");
        localFacultyVacation.Level5DeptDesc.Should().Be("ANIMAL SCIENCE");

        var deansOfficeBalance = balances.Single(balance => balance.EmployeeId == "17628405");
        deansOfficeBalance.Level5Dept.Should().Be("030000");
        deansOfficeBalance.Level5DeptDesc.Should().Be("AGR & ENV SCI DEANS OFFICE");

        balances.Where(balance => balance.EmployeeId == DevelopmentSeedData.LocalFacultyEmployeeId)
            .Max(balance => balance.AsOfDate)
            .Should().Be(new DateOnly(2026, 7, 12));
        balances.Where(balance => balance.EmployeeId == "66510837")
            .Max(balance => balance.AsOfDate)
            .Should().Be(new DateOnly(2026, 6, 30));
    }

    private static async Task SeedAccrualBalancesAsync(AppDbContext db)
    {
        db.Set<EmployeeAccrualBalance>().AddRange(AccrualSeeds.Select(CreateEmployeeAccrualBalance));
        await db.SaveChangesAsync();
    }

    private static EmployeeAccrualBalance CreateEmployeeAccrualBalance(EmployeeAccrualBalanceSeed seed)
    {
        var asOfDate = DateOnly.Parse(seed.AsOfDate);
        var loadedAt = DateTime.SpecifyKind(DateTime.Parse($"{seed.AsOfDate}T14:00:00"), DateTimeKind.Utc);

        return new EmployeeAccrualBalance
        {
            EmployeeId = seed.EmployeeId,
            AsOfDate = asOfDate,
            PositionNumber = seed.PositionNumber,
            LeaveTypeNumber = seed.LeaveTypeNumber,
            EmployeeEmail = seed.EmployeeEmail,
            EmployeeName = seed.EmployeeName,
            UnionCode = "99",
            UnionDescription = "Non-Represented",
            EmployeeClassCode = seed.EmployeeClassCode,
            EmployeeClassDescription = seed.EmployeeClassDescription,
            JobCode = seed.JobCode,
            JobCodeDescription = seed.JobCodeDescription,
            ReportsToPositionNumber = "40000001",
            ReportsToEmployeeId = "84726195",
            ReportsToEmployeeName = "Maya Thompson",
            HrStatus = "A",
            EmployeeStatus = "A",
            EmployeeStatusDescription = "Active",
            EmployeeType = "E",
            EmployeeTypeDescription = "Employee",
            HourlyRateFTE = 1.0000m,
            TypeLabel = seed.TypeLabel,
            PrevBal = seed.PrevBal,
            HoursTaken = seed.HoursTaken,
            AccrualHours = seed.AccrualHours,
            AdjustedHours = seed.AdjustedHours,
            CalculatedBal = seed.CalculatedBal,
            AccrualLimit = seed.AccrualLimit,
            ApproachingMax = seed.AccrualLimit > 0m && seed.CalculatedBal >= seed.AccrualLimit * 0.9m ? "Y" : "N",
            HoursOverUnderPolicyMax = seed.AccrualLimit > 0m ? seed.AccrualLimit - seed.CalculatedBal : 0.00m,
            AccrualPercentage = seed.AccrualLimit > 0m ? decimal.Round(seed.CalculatedBal / seed.AccrualLimit * 100m, 2) : 0.00m,
            ExceptionalMaxVacationOnly = 0,
            Level1Dept = "DVCMP",
            Level1DeptDesc = "UC Davis Campus",
            Level2Dept = "DVCMP",
            Level2DeptDesc = "UC DAVIS CAMPUS",
            Level3Dept = "01",
            Level3DeptDesc = "AGRICULTURE",
            Level4Dept = "S2000",
            Level4DeptDesc = "AGRICULTURE SUBDIV",
            Level5Dept = seed.Level5Dept,
            Level5DeptDesc = seed.Level5DeptDesc,
            LoadDate = loadedAt,
            LastUpdated = loadedAt,
        };
    }

    private static readonly EmployeeAccrualBalanceSeed[] AccrualSeeds =
    [
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-06-28", "40001234", 10, "Vacation", 88.00m, 0.00m, 8.00m, 0.00m, 96.00m, 240.00m, "001700", "Professor", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-07-12", "40001234", 10, "Vacation", 96.00m, 8.00m, 8.00m, 0.00m, 96.00m, 240.00m, "001700", "Professor", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-07-12", "40001234", 20, "Sick Leave", 280.00m, 0.00m, 8.00m, 0.00m, 288.00m, 0.00m, "001700", "Professor", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new("66510837", "lwilson@fake.ucdavis.edu", "Lena Wilson", "2026-06-30", "40002345", 10, "Vacation", 160.00m, 0.00m, 8.00m, 0.00m, 168.00m, 240.00m, "001700", "Professor", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new("66510837", "lwilson@fake.ucdavis.edu", "Lena Wilson", "2026-06-30", "40002345", 20, "Sick Leave", 272.00m, 0.00m, 8.00m, 0.00m, 280.00m, 0.00m, "001700", "Professor", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new("36190428", "apatel@fake.ucdavis.edu", "Asha Patel", "2026-06-30", "40003456", 10, "Vacation", 210.00m, 8.00m, 10.00m, 0.00m, 212.00m, 240.00m, "000245", "Department Chair", "000245", "Department Chair", "030045", "ANIMAL SCIENCE"),
        new("17628405", "sbaker@fake.ucdavis.edu", "Sofia Baker", "2026-07-12", "40004567", 50, "Compensatory Time", 18.00m, 0.00m, 2.00m, 0.00m, 20.00m, 80.00m, "006257", "Agricultural Technician", "006257", "Agricultural Technician", "030000", "AGR & ENV SCI DEANS OFFICE"),
    ];

    private sealed record EmployeeAccrualBalanceSeed(
        string EmployeeId,
        string EmployeeEmail,
        string EmployeeName,
        string AsOfDate,
        string PositionNumber,
        int LeaveTypeNumber,
        string TypeLabel,
        decimal PrevBal,
        decimal HoursTaken,
        decimal AccrualHours,
        decimal AdjustedHours,
        decimal CalculatedBal,
        decimal AccrualLimit,
        string EmployeeClassCode,
        string EmployeeClassDescription,
        string JobCode,
        string JobCodeDescription,
        string Level5Dept,
        string Level5DeptDesc);
}
