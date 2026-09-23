using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using server.core.Migrations;

namespace Server.Tests.Data;

public class FacultyViewsMigrationTests
{
    [Fact]
    public void Up_creates_both_faculty_views_then_removes_only_the_obsolete_views()
    {
        var operations = new TestableMigration().BuildOperations(up: true);
        operations.Should().HaveCount(4).And.OnlyContain(operation => !operation.SuppressTransaction);
        var faculty = ExtractDefinition(operations[0].Sql);
        var balances = ExtractDefinition(operations[1].Sql);
        faculty.Should().StartWith("CREATE OR ALTER VIEW [dbo].[vw_CurrentFacultyWithAccrual]");
        balances.Should().StartWith("CREATE OR ALTER VIEW [dbo].[vw_CurrentFacultyAccrualBalance]");
        foreach (var sql in new[] { faculty, balances })
        {
            sql.Should().Contain("WHERE [person].[IsEmployee] = CAST(1 AS bit)")
                .And.Contain("AND [person].[IsFaculty] = CAST(1 AS bit)")
                .And.Contain("INNER JOIN [LatestEmployeeAccrualDate] AS [latest]")
                .And.NotContain("LEFT JOIN [LatestEmployeeAccrualDate]")
                .And.Contain("MAX([AsOfDate]) AS [LatestAsOfDate]")
                .And.Contain("GROUP BY [EmployeeId]");
        }
        faculty.Should().Contain("[person].[IsFaculty] AS [IsFaculty]").And.NotContain("HasCurrentAccrualRecord");
        faculty.Should().Contain("ROW_NUMBER() OVER")
            .And.Contain("[position].[PositionRank] = 1")
            .And.Contain("AT TIME ZONE 'Pacific Standard Time'")
            .And.Contain("[override].[EffectiveStartDate] <= [pacificDate].[BusinessDate]")
            .And.Contain("[pacificDate].[BusinessDate] < [override].[EffectiveEndDateExclusive]")
            .And.Contain("[override].[EmployeeReportingDepartmentOverrideId] DESC");
        faculty.IndexOf("[position].[Level5Dept]", StringComparison.Ordinal).Should()
            .BeLessThan(faculty.IndexOf("[position].[Level4Dept]", StringComparison.Ordinal));
        faculty.IndexOf("[position].[Level4Dept]", StringComparison.Ordinal).Should()
            .BeLessThan(faculty.IndexOf("[position].[Level3Dept]", StringComparison.Ordinal));
        balances.Should().Contain("MAX([balance].[CalculatedBal]) AS [CalculatedBal]")
            .And.NotContain("SUM(")
            .And.Contain("COUNT(*) AS [PositionRowCount]")
            .And.Contain("MIN([balance].[CalculatedBal]) AS [MinCalculatedBal]")
            .And.Contain("MAX([balance].[CalculatedBal]) AS [MaxCalculatedBal]")
            .And.Contain("AS [HasDivergentPositionBalances]")
            .And.Contain("AND [balance].[AsOfDate] = [latest].[LatestAsOfDate]");
        operations.Skip(2).Select(operation => operation.Sql).Should().Equal(
            "DROP VIEW IF EXISTS [dbo].[vw_CurrentEmployee];",
            "DROP VIEW IF EXISTS [dbo].[vw_CurrentAccrualBalance];");
    }

    [Fact]
    public void Down_restores_the_exact_previous_definitions_then_removes_the_faculty_views()
    {
        var operations = new TestableMigration().BuildOperations(up: false);
        operations.Should().HaveCount(4).And.OnlyContain(operation => !operation.SuppressTransaction);
        operations[0].Sql.Should().Be(new OriginalEmployeeMigration().Definition());
        operations[1].Sql.Should().Be(new OriginalBalanceMigration().Definition());
        operations.Skip(2).Select(operation => operation.Sql).Should().Equal(
            "DROP VIEW IF EXISTS [dbo].[vw_CurrentFacultyWithAccrual];",
            "DROP VIEW IF EXISTS [dbo].[vw_CurrentFacultyAccrualBalance];");
    }

    private sealed class TestableMigration : ReplaceCurrentViewsWithFacultyViews
    {
        public IReadOnlyList<SqlOperation> BuildOperations(bool up)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            if (up) Up(builder);
            else Down(builder);
            return builder.Operations.Cast<SqlOperation>().ToArray();
        }
    }

    private sealed class OriginalEmployeeMigration : AddCurrentEmployeeView
    {
        public string Definition()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return ((SqlOperation)builder.Operations.Single()).Sql;
        }
    }

    private sealed class OriginalBalanceMigration : AddCurrentAccrualBalanceView
    {
        public string Definition()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return ((SqlOperation)builder.Operations.Single()).Sql;
        }
    }

    private static string ExtractDefinition(string sql)
    {
        sql.Should().StartWith("EXEC(N'").And.EndWith("');");
        return sql[7..^3].Replace("''", "'");
    }
}
