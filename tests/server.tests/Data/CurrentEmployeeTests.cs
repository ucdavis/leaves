using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class CurrentEmployeeTests
{
    [Fact]
    public void ModelMapsCurrentEmployeeAsAKeylessReadOnlyView()
    {
        using var db = CreateSqlServerContext();

        var entity = db.Model.FindEntityType(typeof(CurrentEmployee));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetViewName().Should().Be("vw_CurrentEmployee");
        entity.GetViewSchema().Should().Be("dbo");
        entity.FindProperty(nameof(CurrentEmployee.IamId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentEmployee.LatestAsOfDate))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(CurrentEmployee.ReportingDepartmentOverrideId))!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void IamIdLookupTranslatesToAViewQuery()
    {
        using var db = CreateSqlServerContext();

        var sql = db.CurrentEmployees
            .Where(employee => employee.IamId == "sbaker")
            .ToQueryString();

        sql.Should().Contain("FROM [dbo].[vw_CurrentEmployee]");
        sql.Should().Contain("WHERE [v].[IamId] = 'sbaker'");
    }

    private static AppDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMetadataOnly;User ID=sa;Password=not-used")
            .Options;

        return new AppDbContext(options);
    }
}
