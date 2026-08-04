using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Tests.Data;

public class CurrentAccrualBalanceTests
{
    [Fact]
    public void ModelMapsCurrentAccrualBalanceAsAKeylessReadOnlyView()
    {
        using var db = CreateSqlServerContext();

        var entity = db.Model.FindEntityType(typeof(CurrentAccrualBalance));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetViewName().Should().Be("vw_CurrentAccrualBalance");
        entity.GetViewSchema().Should().Be("dbo");
        entity.FindProperty(nameof(CurrentAccrualBalance.IamId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentAccrualBalance.EmployeeId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(CurrentAccrualBalance.TypeLabel))!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void IamIdLookupTranslatesToAViewQuery()
    {
        using var db = CreateSqlServerContext();

        var sql = db.CurrentAccrualBalances
            .Where(balance => balance.IamId == "sbaker")
            .ToQueryString();

        sql.Should().Contain("FROM [dbo].[vw_CurrentAccrualBalance]");
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
