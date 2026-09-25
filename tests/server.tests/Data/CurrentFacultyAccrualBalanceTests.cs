using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class CurrentFacultyAccrualBalanceTests
{
    [Fact]
    public void ModelMapsCurrentFacultyAccrualBalanceAsAKeylessReadOnlyView()
    {
        using var db = CreateSqlServerContext();

        var entity = db.Model.FindEntityType(typeof(CurrentFacultyAccrualBalance));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetViewName().Should().Be("vw_CurrentFacultyAccrualBalance");
        entity.GetViewSchema().Should().Be("dbo");
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.IamId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.EmployeeId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.TypeLabel))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.IamId))!.GetColumnType().Should().Be("char(10)");
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.CalculatedBal))!.GetPrecision().Should().Be(10);
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.CalculatedBal))!.GetScale().Should().Be(2);
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.AccrualPercentage))!.GetPrecision().Should().Be(7);
        entity.FindProperty(nameof(CurrentFacultyAccrualBalance.AccrualPercentage))!.GetScale().Should().Be(2);
    }

    private static AppDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMetadataOnly;User ID=sa;Password=not-used")
            .Options;

        return new AppDbContext(options);
    }
}
