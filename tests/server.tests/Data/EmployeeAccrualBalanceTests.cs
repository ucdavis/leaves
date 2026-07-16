using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task DevelopmentSeedIsIdempotentAndQueryIsNoTracking()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync(includeDevSeed: true);
        await initializer.InitializeAsync(includeDevSeed: true);

        db.ChangeTracker.Clear();
        var balances = await db.EmployeeAccrualBalances.ToListAsync();

        balances.Should().HaveCount(7);
        db.ChangeTracker.Entries<EmployeeAccrualBalance>().Should().BeEmpty();

        var localRequesterVacation = balances.Single(balance =>
            balance.EmployeeId == DevelopmentSeedData.LocalRequesterEmployeeId &&
            balance.AsOfDate == new DateOnly(2026, 7, 12) &&
            balance.LeaveTypeNumber == 10);

        localRequesterVacation.ApproachingMax.Should().Be("N");
        localRequesterVacation.HoursOverUnderPolicyMax.Should().Be(152.62m);
        localRequesterVacation.AccrualPercentage.Should().Be(36.41m);
        localRequesterVacation.Level5Dept.Should().Be("030045");
        localRequesterVacation.Level5DeptDesc.Should().Be("ANIMAL SCIENCE");

        var deansOfficeBalance = balances.Single(balance => balance.EmployeeId == "17628405");
        deansOfficeBalance.Level5Dept.Should().Be("030000");
        deansOfficeBalance.Level5DeptDesc.Should().Be("AGR & ENV SCI DEANS OFFICE");

        balances.Where(balance => balance.EmployeeId == DevelopmentSeedData.LocalRequesterEmployeeId)
            .Max(balance => balance.AsOfDate)
            .Should().Be(new DateOnly(2026, 7, 12));
        balances.Where(balance => balance.EmployeeId == "66510837")
            .Max(balance => balance.AsOfDate)
            .Should().Be(new DateOnly(2026, 6, 30));
    }
}
