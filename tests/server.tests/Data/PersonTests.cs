using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class PersonTests
{
    [Fact]
    public void HumanNameAndPronounColumnsSupportUnicode()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMetadataOnly;User ID=sa;Password=not-used")
            .Options;
        using var db = new AppDbContext(options);

        var entity = db.Model.FindEntityType(typeof(Person));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(Person.FirstName))!.GetColumnType().Should().Be("nvarchar(64)");
        entity.FindProperty(nameof(Person.MiddleName))!.GetColumnType().Should().Be("nvarchar(64)");
        entity.FindProperty(nameof(Person.LastName))!.GetColumnType().Should().Be("nvarchar(64)");
        entity.FindProperty(nameof(Person.Suffix))!.GetColumnType().Should().Be("nvarchar(16)");
        entity.FindProperty(nameof(Person.FullName))!.GetColumnType().Should().Be("nvarchar(128)");
        entity.FindProperty(nameof(Person.Pronouns))!.GetColumnType().Should().Be("nvarchar(64)");

        db.Model.GetEntityTypes()
            .Should().NotContain(modelEntity => modelEntity.GetTableName() == "People_Staging");
    }
}
