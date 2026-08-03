using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using server.core.Migrations;

namespace Server.Tests.Data;

public class CurrentAccrualBalanceViewMigrationTests
{
    [Fact]
    public void UpCreatesCurrentAccrualBalanceViewWithAgreedContract()
    {
        var operation = new TestableMigration().BuildUpOperations()
            .Single()
            .Should().BeOfType<SqlOperation>().Which;
        var sql = ExtractViewDefinition(operation.Sql);

        sql.Should().Contain("CREATE OR ALTER VIEW [dbo].[vw_CurrentAccrualBalance]");
        sql.Should().Contain("FROM [dbo].[People] AS [person]");
        sql.Should().Contain("INNER JOIN [dbo].[EmployeeAccrualBalances] AS [balance]");
        sql.Should().Contain("WHERE [person].[IsEmployee] = CAST(1 AS bit)");

        sql.Should().Contain("MAX([AsOfDate]) AS [LatestAsOfDate]");
        sql.Should().Contain("GROUP BY [EmployeeId]");
        sql.Should().Contain("[balance].[AsOfDate] = [latest].[LatestAsOfDate]");
        sql.Should().Contain("[balance].[LeaveTypeNumber]");

        sql.Should().Contain("MAX([balance].[CalculatedBal]) AS [CalculatedBal]");
        sql.Should().Contain("COUNT(*) AS [PositionRowCount]");
        sql.Should().Contain("MIN([balance].[CalculatedBal]) AS [MinCalculatedBal]");
        sql.Should().Contain("MAX([balance].[CalculatedBal]) AS [MaxCalculatedBal]");
        sql.Should().Contain("MIN([balance].[CalculatedBal]) <> MAX([balance].[CalculatedBal])");
        sql.Should().NotContain("SUM([balance].[CalculatedBal])");

        var outputAliases = new[]
        {
            "[IamId]", "[EmployeeId]", "[LatestAsOfDate]", "[LeaveTypeNumber]",
            "[TypeLabel]", "[CalculatedBal]", "[AccrualLimit]", "[ApproachingMax]",
            "[AccrualPercentage]", "[PositionRowCount]", "[MinCalculatedBal]",
            "[MaxCalculatedBal]", "[HasDivergentPositionBalances]",
        };

        foreach (var outputAlias in outputAliases)
        {
            sql.Should().Contain($"AS {outputAlias}");
        }
    }

    [Fact]
    public void DownDropsOnlyCurrentAccrualBalanceViewWhenItExists()
    {
        var operation = new TestableMigration().BuildDownOperations()
            .Single()
            .Should().BeOfType<SqlOperation>().Which;

        operation.Sql.Should().Be("DROP VIEW IF EXISTS [dbo].[vw_CurrentAccrualBalance];");
    }

    private sealed class TestableMigration : AddCurrentAccrualBalanceView
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }

    private static string ExtractViewDefinition(string migrationSql)
    {
        const string prefix = "EXEC(N'";
        const string suffix = "');";

        migrationSql.Should().StartWith(prefix).And.EndWith(suffix);

        return migrationSql[prefix.Length..^suffix.Length].Replace("''", "'");
    }
}
