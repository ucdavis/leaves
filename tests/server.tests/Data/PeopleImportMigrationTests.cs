using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using server.core.Migrations;

namespace Server.Tests.Data;

public class PeopleImportMigrationTests
{
    [Fact]
    public void UpCreatesUnicodeStagingTableAndTransactionalPromotionProcedure()
    {
        var operations = new TestableMigration().BuildUpOperations();

        var alteredColumns = operations.OfType<AlterColumnOperation>().ToList();
        alteredColumns.Should().HaveCount(6);
        alteredColumns.Should().AllSatisfy(column =>
        {
            column.Table.Should().Be("People");
            column.Schema.Should().Be("dbo");
            column.ColumnType.Should().StartWith("nvarchar(");
            column.OldColumn!.ColumnType.Should().StartWith("varchar(");
        });

        var table = operations.OfType<CreateTableOperation>().Single();
        var targetTable = new TestableTargetMigration().BuildUpOperations()
            .OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "People");
        table.Name.Should().Be("People_Staging");
        table.Schema.Should().Be("dbo");
        table.Columns.Select(column => ColumnContract(column, useUnicodePersonColumns: false))
            .Should().Equal(targetTable.Columns.Select(column => ColumnContract(column, useUnicodePersonColumns: true)));
        table.PrimaryKey!.Name.Should().Be("PK_People_Staging");
        table.PrimaryKey.Columns.Should().Equal(targetTable.PrimaryKey!.Columns);

        table.Columns.Single(column => column.Name == "IamId").ColumnType.Should().Be("char(10)");
        table.Columns.Single(column => column.Name == "IamId").IsNullable.Should().BeFalse();
        table.Columns.Single(column => column.Name == "FirstName").ColumnType.Should().Be("nvarchar(64)");
        table.Columns.Single(column => column.Name == "MiddleName").ColumnType.Should().Be("nvarchar(64)");
        table.Columns.Single(column => column.Name == "LastName").ColumnType.Should().Be("nvarchar(64)");
        table.Columns.Single(column => column.Name == "Suffix").ColumnType.Should().Be("nvarchar(16)");
        table.Columns.Single(column => column.Name == "FullName").ColumnType.Should().Be("nvarchar(128)");
        table.Columns.Single(column => column.Name == "Pronouns").ColumnType.Should().Be("nvarchar(64)");

        var sql = ExtractProcedureDefinition(operations.OfType<SqlOperation>().Single().Sql);
        sql.Should().Contain("CREATE OR ALTER PROCEDURE [dbo].[usp_PromotePeople]");
        sql.Should().Contain("WITH EXECUTE AS OWNER");
        sql.Should().Contain("SET XACT_ABORT ON");
        sql.Should().Contain("BEGIN TRANSACTION");
        sql.Should().Contain("FROM [dbo].[People_Staging] WITH (TABLOCKX, HOLDLOCK)");
        sql.Should().Contain("IF @StagingRowCount = 0");
        sql.Should().Contain("TRUNCATE TABLE [dbo].[People]");
        sql.Should().Contain("INSERT INTO [dbo].[People]");
        sql.Should().Contain("IF @InsertedRowCount <> @StagingRowCount");
        sql.Should().Contain("ROLLBACK TRANSACTION");
        sql.Should().Contain("THROW;");
        sql.Should().NotContain("SELECT *");
        sql.Should().NotContain("TRUNCATE TABLE [dbo].[People_Staging]");

        foreach (var column in table.Columns)
        {
            sql.Split($"[{column.Name}]", StringSplitOptions.None).Should().HaveCount(3);
        }
    }

    private static object ColumnContract(AddColumnOperation column, bool useUnicodePersonColumns)
    {
        var unicodeColumnTypes = new Dictionary<string, string>
        {
            ["FirstName"] = "nvarchar(64)",
            ["MiddleName"] = "nvarchar(64)",
            ["LastName"] = "nvarchar(64)",
            ["Suffix"] = "nvarchar(16)",
            ["FullName"] = "nvarchar(128)",
            ["Pronouns"] = "nvarchar(64)",
        };
        var columnType = useUnicodePersonColumns && unicodeColumnTypes.TryGetValue(column.Name, out var unicodeColumnType)
            ? unicodeColumnType
            : column.ColumnType;

        return new
        {
            column.Name,
            column.ClrType,
            ColumnType = columnType,
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

    [Fact]
    public void DownDropsImportObjectsAndRestoresNonUnicodeColumns()
    {
        var operations = new TestableMigration().BuildDownOperations();

        operations.OfType<SqlOperation>().Single().Sql
            .Should().Be("DROP PROCEDURE IF EXISTS [dbo].[usp_PromotePeople];");
        operations.OfType<DropTableOperation>().Single().Name.Should().Be("People_Staging");

        var alteredColumns = operations.OfType<AlterColumnOperation>().ToList();
        alteredColumns.Should().HaveCount(6);
        alteredColumns.Should().AllSatisfy(column =>
        {
            column.ColumnType.Should().StartWith("varchar(");
            column.OldColumn!.ColumnType.Should().StartWith("nvarchar(");
        });
    }

    private sealed class TestableMigration : AddPeopleImportSupport
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

    private sealed class TestableTargetMigration : AddCoreDomainTables
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }
}
