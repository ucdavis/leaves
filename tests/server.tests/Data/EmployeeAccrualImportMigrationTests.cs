using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using server.core.Migrations;

namespace Server.Tests.Data;

public class EmployeeAccrualImportMigrationTests
{
    [Fact]
    public void UpCreatesStagingTableWithTargetSchemaAndTransactionalPromotionProcedure()
    {
        var operations = new TestableImportMigration().BuildUpOperations();
        var stagingTable = operations.OfType<CreateTableOperation>().Single();
        var targetTable = new TestableTargetMigration().BuildUpOperations()
            .OfType<CreateTableOperation>()
            .Single();

        stagingTable.Name.Should().Be("EmployeeAccrualBalances_Staging");
        stagingTable.Schema.Should().Be("dbo");
        stagingTable.Columns.Select(ColumnContract)
            .Should().Equal(targetTable.Columns.Select(ColumnContract));
        stagingTable.PrimaryKey!.Name.Should().Be("PK_EmployeeAccrualBalances_Staging");
        stagingTable.PrimaryKey.Columns.Should().Equal(targetTable.PrimaryKey!.Columns);

        var indexes = operations.OfType<CreateIndexOperation>().ToList();
        indexes.Should().HaveCount(2);
        indexes.Single(index => index.Name == "IX_EmployeeAccrualBalances_Staging_EmployeeId")
            .Columns.Should().Equal("EmployeeId");
        indexes.Single(index => index.Name == "IX_EmployeeAccrualBalances_Staging_AsOf_Employee_LeaveType")
            .Columns.Should().Equal("AsOfDate", "EmployeeId", "LeaveTypeNumber");

        var sql = ExtractProcedureDefinition(operations.OfType<SqlOperation>().Single().Sql);
        sql.Should().Contain("CREATE OR ALTER PROCEDURE [dbo].[usp_PromoteEmployeeAccrualBalances]");
        sql.Should().Contain("WITH EXECUTE AS OWNER");
        sql.Should().Contain("SET XACT_ABORT ON");
        sql.Should().Contain("BEGIN TRANSACTION");
        sql.Should().Contain("FROM [dbo].[EmployeeAccrualBalances_Staging] WITH (TABLOCKX, HOLDLOCK)");
        sql.Should().Contain("IF @StagingRowCount = 0");
        sql.Should().Contain("TRUNCATE TABLE [dbo].[EmployeeAccrualBalances]");
        sql.Should().Contain("INSERT INTO [dbo].[EmployeeAccrualBalances]");
        sql.Should().Contain("IF @InsertedRowCount <> @StagingRowCount");
        sql.Should().Contain("ROLLBACK TRANSACTION");
        sql.Should().Contain("THROW;");
        sql.Should().NotContain("SELECT *");
        sql.Should().NotContain("TRUNCATE TABLE [dbo].[EmployeeAccrualBalances_Staging]");

        foreach (var column in stagingTable.Columns)
        {
            sql.Split($"[{column.Name}]", StringSplitOptions.None).Should().HaveCount(3);
        }
    }

    [Fact]
    public void DownDropsOnlyEmployeeAccrualImportObjects()
    {
        var operations = new TestableImportMigration().BuildDownOperations();

        operations.Should().HaveCount(2);
        operations.OfType<SqlOperation>().Single().Sql
            .Should().Be("DROP PROCEDURE IF EXISTS [dbo].[usp_PromoteEmployeeAccrualBalances];");

        var dropTable = operations.OfType<DropTableOperation>().Single();
        dropTable.Name.Should().Be("EmployeeAccrualBalances_Staging");
        dropTable.Schema.Should().Be("dbo");
    }

    private static object ColumnContract(AddColumnOperation column)
    {
        return new
        {
            column.Name,
            column.ClrType,
            column.ColumnType,
            column.MaxLength,
            column.Precision,
            column.Scale,
            column.IsNullable,
        };
    }

    private static string ExtractProcedureDefinition(string migrationSql)
    {
        const string prefix = "EXEC(N'";
        const string suffix = "');";

        migrationSql.Should().StartWith(prefix).And.EndWith(suffix);

        return migrationSql[prefix.Length..^suffix.Length].Replace("''", "'");
    }

    private sealed class TestableImportMigration : AddEmployeeAccrualImportSupport
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

    private sealed class TestableTargetMigration : AddEmployeeAccrualBalances
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }
}
