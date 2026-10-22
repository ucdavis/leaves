using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsyncWithoutDevelopmentSeedCreatesDatabaseWithoutSeedRows()
    {
        await using var db = CreateUninitializedInMemoryContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync(includeDevSeed: false);

        (await db.Database.EnsureCreatedAsync()).Should().BeFalse();
        (await db.AppUsers.CountAsync()).Should().Be(0);
        (await db.People.CountAsync()).Should().Be(0);
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
    }

    [Fact]
    public async Task InitializeAsyncWithDevelopmentSeedDoesNotDuplicatePaddedDepartmentCode()
    {
        await using var db = CreateUninitializedInMemoryContext();
        await db.Database.EnsureCreatedAsync();
        db.Departments.Add(new Department
        {
            DepartmentCode = $"{DevelopmentSeedData.TestDepartmentCode} ",
            DepartmentName = DevelopmentSeedData.TestDepartmentName,
        });
        await db.SaveChangesAsync();

        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync(includeDevSeed: true);

        (await db.Departments.CountAsync()).Should().Be(3);
        (await db.Departments.CountAsync(department =>
            department.DepartmentCode.Trim() == DevelopmentSeedData.TestDepartmentCode)).Should().Be(1);
    }

    private static AppDbContext CreateUninitializedInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DbInitializerTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
