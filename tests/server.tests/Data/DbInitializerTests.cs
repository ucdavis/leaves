using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;

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

    private static AppDbContext CreateUninitializedInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DbInitializerTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
