using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;

namespace Server.Tests.Data;

public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsyncWithoutDevelopmentSeedCreatesTheRequiredLeaveTypeCatalog()
    {
        await using var db = CreateUninitializedInMemoryContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync(includeDevSeed: false);
        await initializer.InitializeAsync(includeDevSeed: false);

        (await db.Database.EnsureCreatedAsync()).Should().BeFalse();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.People.CountAsync()).Should().Be(0);
        (await db.LeaveTypes
            .OrderBy(leaveType => leaveType.LeaveTypeKey)
            .Select(leaveType => new
            {
                leaveType.LeaveTypeKey,
                leaveType.SourceLeaveTypeNumber,
                leaveType.DisplayName,
                leaveType.HasAccrualBalance,
                leaveType.IsActive,
            })
            .ToListAsync())
            .Should()
            .BeEquivalentTo([
                new
                {
                    LeaveTypeKey = "FamilyCare",
                    SourceLeaveTypeNumber = (int?)30,
                    DisplayName = "FMLA",
                    HasAccrualBalance = false,
                    IsActive = true,
                },
                new
                {
                    LeaveTypeKey = "ProfessionalDevelopment",
                    SourceLeaveTypeNumber = (int?)null,
                    DisplayName = "Professional Development",
                    HasAccrualBalance = false,
                    IsActive = true,
                },
                new
                {
                    LeaveTypeKey = "Sabbatical",
                    SourceLeaveTypeNumber = (int?)40,
                    DisplayName = "Sabbatical",
                    HasAccrualBalance = false,
                    IsActive = true,
                },
                new
                {
                    LeaveTypeKey = "Sick",
                    SourceLeaveTypeNumber = (int?)20,
                    DisplayName = "Sick Leave",
                    HasAccrualBalance = true,
                    IsActive = true,
                },
                new
                {
                    LeaveTypeKey = "Vacation",
                    SourceLeaveTypeNumber = (int?)10,
                    DisplayName = "Vacation",
                    HasAccrualBalance = true,
                    IsActive = true,
                },
            ]);
    }

    [Fact]
    public async Task InitializeAsyncWithDevelopmentSeedCreatesDatabaseAndSeedsRows()
    {
        await using var db = CreateUninitializedInMemoryContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync(includeDevSeed: true);

        (await db.Database.EnsureCreatedAsync()).Should().BeFalse();
        (await db.AppUsers.CountAsync()).Should().BeGreaterThan(0);
        (await db.People.CountAsync()).Should().BeGreaterThan(0);
        (await db.LeaveTypes.CountAsync()).Should().Be(5);
    }

    private static AppDbContext CreateUninitializedInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DbInitializerTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
