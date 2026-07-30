using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class AdminStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsyncAggregatesCurrentRowsWithoutLoadingHistory()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);
        await initializer.InitializeAsync(seedDevelopmentData: true);

        db.ChangeTracker.Clear();
        var currentBalance = await db.EmployeeAccrualBalances.SingleAsync(balance =>
            balance.EmployeeId == "36190428" &&
            balance.AsOfDate == new DateOnly(2026, 6, 30) &&
            balance.LeaveTypeNumber == 10);
        var historicalBalance = (EmployeeAccrualBalance)db.Entry(currentBalance).CurrentValues.ToObject();
        historicalBalance.AsOfDate = currentBalance.AsOfDate.AddDays(-14);
        historicalBalance.ApproachingMax = "Y";
        historicalBalance.HoursOverUnderPolicyMax = 12m;
        historicalBalance.LastUpdated = currentBalance.LastUpdated.AddDays(-14);
        db.Set<EmployeeAccrualBalance>().Add(historicalBalance);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AdminStatusService(new AdminDirectoryService(db));

        var response = await service.GetStatusAsync(CancellationToken.None);

        response.ClusterCount.Should().Be(2);
        response.ClustersMissingCaos.Should().Be(0);
        response.DepartmentCount.Should().Be(2);
        response.DepartmentsMissingChairs.Should().Be(1);
        response.StatusSnapshot.Issues.Should().BeEquivalentTo(new AdminIssuesResponse(
            ApproachingVacationCap: 0,
            FacultyAtVacationCap: 3,
            PendingRequests: 2));

        response.DataSources.Should().ContainSingle(source =>
            source.Id == "db-people" &&
            source.UpdatedAt != null);
        response.DataSources.Should().ContainSingle(source =>
            source.Id == "db-accruals" &&
            source.Status == "ready" &&
            source.UpdatedAt != null);
    }
}
