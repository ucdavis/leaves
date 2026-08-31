using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddPeopleImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Suffix",
                schema: "dbo",
                table: "People",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Pronouns",
                schema: "dbo",
                table: "People",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                schema: "dbo",
                table: "People",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                schema: "dbo",
                table: "People",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "dbo",
                table: "People",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                schema: "dbo",
                table: "People",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "People_Staging",
                schema: "dbo",
                columns: table => new
                {
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    EmployeeId = table.Column<string>(type: "char(8)", maxLength: 8, nullable: true),
                    StudentId = table.Column<string>(type: "char(9)", maxLength: 9, nullable: true),
                    ExternalId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Pronouns = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsEmployee = table.Column<bool>(type: "bit", nullable: true),
                    IsHsEmployee = table.Column<bool>(type: "bit", nullable: true),
                    IsFaculty = table.Column<bool>(type: "bit", nullable: true),
                    IsStudent = table.Column<bool>(type: "bit", nullable: true),
                    IsStaff = table.Column<bool>(type: "bit", nullable: true),
                    IsExternal = table.Column<bool>(type: "bit", nullable: true),
                    PrivacyCode = table.Column<string>(type: "char(1)", maxLength: 1, nullable: true),
                    IsCampusEmployee = table.Column<string>(type: "char(1)", maxLength: 1, nullable: true),
                    UserId = table.Column<string>(type: "char(8)", maxLength: 8, nullable: true),
                    Email = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    ModifyDate = table.Column<DateTime>(type: "datetime2(6)", precision: 6, nullable: true),
                    ModifyDateRaw = table.Column<string>(type: "char(19)", maxLength: 19, nullable: true),
                    FirstIngestedAt = table.Column<DateTime>(type: "datetime2(6)", precision: 6, nullable: true),
                    LastFetchedAt = table.Column<DateTime>(type: "datetime2(6)", precision: 6, nullable: true),
                    LastRunId = table.Column<string>(type: "char(36)", maxLength: 36, nullable: true),
                    SourceEndpoint = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    PromotedAt = table.Column<DateTime>(type: "datetime2(6)", precision: 6, nullable: true),
                    PromotionRunId = table.Column<string>(type: "char(36)", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People_Staging", x => x.IamId);
                });

            migrationBuilder.Sql(
                """
                EXEC(N'
                CREATE OR ALTER PROCEDURE [dbo].[usp_PromotePeople]
                WITH EXECUTE AS OWNER
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @StagingRowCount bigint;
                    DECLARE @InsertedRowCount bigint;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        SELECT @StagingRowCount = COUNT_BIG(*)
                        FROM [dbo].[People_Staging] WITH (TABLOCKX, HOLDLOCK);

                        IF @StagingRowCount = 0
                            THROW 50001, ''People staging is empty; promotion was not run.'', 1;

                        TRUNCATE TABLE [dbo].[People];

                        INSERT INTO [dbo].[People]
                        (
                            [IamId],
                            [EmployeeId],
                            [StudentId],
                            [ExternalId],
                            [FirstName],
                            [MiddleName],
                            [LastName],
                            [Suffix],
                            [FullName],
                            [Pronouns],
                            [IsEmployee],
                            [IsHsEmployee],
                            [IsFaculty],
                            [IsStudent],
                            [IsStaff],
                            [IsExternal],
                            [PrivacyCode],
                            [IsCampusEmployee],
                            [UserId],
                            [Email],
                            [ModifyDate],
                            [ModifyDateRaw],
                            [FirstIngestedAt],
                            [LastFetchedAt],
                            [LastRunId],
                            [SourceEndpoint],
                            [PromotedAt],
                            [PromotionRunId]
                        )
                        SELECT
                            [IamId],
                            [EmployeeId],
                            [StudentId],
                            [ExternalId],
                            [FirstName],
                            [MiddleName],
                            [LastName],
                            [Suffix],
                            [FullName],
                            [Pronouns],
                            [IsEmployee],
                            [IsHsEmployee],
                            [IsFaculty],
                            [IsStudent],
                            [IsStaff],
                            [IsExternal],
                            [PrivacyCode],
                            [IsCampusEmployee],
                            [UserId],
                            [Email],
                            [ModifyDate],
                            [ModifyDateRaw],
                            [FirstIngestedAt],
                            [LastFetchedAt],
                            [LastRunId],
                            [SourceEndpoint],
                            [PromotedAt],
                            [PromotionRunId]
                        FROM [dbo].[People_Staging];

                        SET @InsertedRowCount = @@ROWCOUNT;

                        IF @InsertedRowCount <> @StagingRowCount
                            THROW 50002, ''People promotion row count did not match staging.'', 1;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;

                        THROW;
                    END CATCH;
                END;');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[usp_PromotePeople];");

            migrationBuilder.DropTable(
                name: "People_Staging",
                schema: "dbo");

            migrationBuilder.AlterColumn<string>(
                name: "Suffix",
                schema: "dbo",
                table: "People",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Pronouns",
                schema: "dbo",
                table: "People",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                schema: "dbo",
                table: "People",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                schema: "dbo",
                table: "People",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "dbo",
                table: "People",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                schema: "dbo",
                table: "People",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
