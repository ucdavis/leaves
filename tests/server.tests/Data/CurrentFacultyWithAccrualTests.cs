using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class CurrentFacultyWithAccrualTests
{
    [Fact]
    public void ModelMapsCurrentFacultyWithAccrualAsAKeylessReadOnlyView()
    {
        using var db = CreateSqlServerContext();

        var entity = db.Model.FindEntityType(typeof(CurrentFacultyWithAccrual));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetViewName().Should().Be("vw_CurrentFacultyWithAccrual");
        entity.GetViewSchema().Should().Be("dbo");
        entity.FindProperty(nameof(CurrentFacultyWithAccrual.IamId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentFacultyWithAccrual.IamId))!.GetColumnType().Should().Be("char(10)");
        entity.FindProperty(nameof(CurrentFacultyWithAccrual.IsFaculty))!.IsNullable.Should().BeTrue();
        entity.FindProperty("HasCurrentAccrualRecord").Should().BeNull();
        entity.FindProperty(nameof(CurrentFacultyWithAccrual.LatestAsOfDate))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(CurrentFacultyWithAccrual.ReportingDepartmentOverrideId))!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void IamIdLookupTranslatesToAViewQuery()
    {
        using var db = CreateSqlServerContext();

        var sql = db.CurrentFacultyWithAccrual
            .Where(employee => employee.IamId == "sbaker")
            .ToQueryString();

        sql.Should().Contain("FROM [dbo].[vw_CurrentFacultyWithAccrual]");
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
