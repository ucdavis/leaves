using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using server.core.Migrations;

namespace Server.Tests.Data;

public class CurrentEmployeeViewMigrationTests
{
    [Fact]
    public void UpCreatesCurrentEmployeeViewWithAgreedContract()
    {
        var operation = new TestableMigration().BuildUpOperations()
            .Single()
            .Should().BeOfType<SqlOperation>().Which;
        var sql = ExtractViewDefinition(operation.Sql);

        sql.Should().Contain("CREATE OR ALTER VIEW [dbo].[vw_CurrentEmployee]");
        sql.Should().Contain("FROM [dbo].[People] AS [person]");
        sql.Should().Contain("WHERE [person].[IsEmployee] = CAST(1 AS bit)");

        sql.Should().Contain("MAX([AsOfDate]) AS [LatestAsOfDate]");
        sql.Should().Contain("GROUP BY [EmployeeId]");
        sql.Should().Contain("AND [latest].[LatestAsOfDate] = [balance].[AsOfDate]");
        sql.Should().Contain("[balance].[PositionNumber]");
        sql.Should().Contain("PARTITION BY [position].[EmployeeId]");
        sql.Should().Contain("[position].[JobCode]");
        sql.Should().Contain("[position].[EmployeeClassCode]");
        sql.Should().Contain("[position].[PositionNumber]");

        sql.IndexOf("[position].[Level5Dept]", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("[position].[Level4Dept]", StringComparison.Ordinal));
        sql.IndexOf("[position].[Level4Dept]", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("[position].[Level3Dept]", StringComparison.Ordinal));

        sql.Should().Contain("AT TIME ZONE 'Pacific Standard Time'");
        sql.Should().Contain("[override].[EffectiveStartDate] <= [pacificDate].[BusinessDate]");
        sql.Should().Contain("[pacificDate].[BusinessDate] < [override].[EffectiveEndDateExclusive]");
        sql.Should().Contain("COALESCE([activeOverride].[DepartmentCode], [sourceDepartment].[DepartmentCode])");

        var outputAliases = new[]
        {
            "[IamId]", "[EmployeeId]", "[DisplayName]", "[Email]", "[LatestAsOfDate]",
            "[HasCurrentAccrualRecord]", "[HrStatus]", "[EmployeeClassCode]",
            "[EmployeeClassDescription]", "[JobCode]", "[JobCodeDescription]",
            "[SourceDepartmentCode]", "[SourceDepartmentName]",
            "[ResolvedReportingDepartmentCode]", "[ResolvedReportingDepartmentName]",
            "[ReportingDepartmentOverrideId]", "[HasReportingDepartmentOverride]",
        };

        foreach (var outputAlias in outputAliases)
        {
            sql.Should().Contain($"AS {outputAlias}");
        }
    }

    [Fact]
    public void DownDropsOnlyCurrentEmployeeViewWhenItExists()
    {
        var operation = new TestableMigration().BuildDownOperations()
            .Single()
            .Should().BeOfType<SqlOperation>().Which;

        operation.Sql.Should().Be("DROP VIEW IF EXISTS [dbo].[vw_CurrentEmployee];");
    }

    private sealed class TestableMigration : AddCurrentEmployeeView
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
