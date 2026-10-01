using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonIsActiveInIam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActiveInIam",
                schema: "dbo",
                table: "People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActiveInIam",
                schema: "dbo",
                table: "People_Staging",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[usp_PromotePeople]'));
                DECLARE @updatedDefinition nvarchar(max);

                IF @definition IS NULL
                    THROW 50003, 'People promotion procedure was not found.', 1;

                SET @updatedDefinition = REPLACE(
                    @definition,
                    N'[Pronouns],',
                    N'[Pronouns], [IsActiveInIam],');

                IF @updatedDefinition = @definition
                    THROW 50004, 'People promotion procedure did not contain the expected column list.', 1;

                SET @updatedDefinition = REPLACE(@updatedDefinition, N'CREATE', N'ALTER');
                EXEC sys.sp_executesql @updatedDefinition;
                """);

            migrationBuilder.Sql(
                """
                DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[vw_CurrentFacultyWithAccrual]'));
                DECLARE @updatedDefinition nvarchar(max);

                IF @definition IS NULL
                    THROW 50005, 'Current faculty with accrual view was not found.', 1;

                SET @updatedDefinition = REPLACE(
                    @definition,
                    N'[latest].[LatestAsOfDate] AS [LatestAsOfDate],',
                    N'[latest].[LatestAsOfDate] AS [LatestAsOfDate], [person].[IsActiveInIam] AS [IsActiveInIam],');

                IF @updatedDefinition = @definition
                    THROW 50006, 'Current faculty with accrual view did not contain the expected projection.', 1;

                SET @updatedDefinition = REPLACE(@updatedDefinition, N'CREATE', N'ALTER');
                EXEC sys.sp_executesql @updatedDefinition;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[vw_CurrentFacultyWithAccrual]'));
                DECLARE @updatedDefinition nvarchar(max);

                IF @definition IS NULL
                    THROW 50007, 'Current faculty with accrual view was not found.', 1;

                SET @updatedDefinition = REPLACE(
                    @definition,
                    N'[latest].[LatestAsOfDate] AS [LatestAsOfDate], [person].[IsActiveInIam] AS [IsActiveInIam],',
                    N'[latest].[LatestAsOfDate] AS [LatestAsOfDate],');

                IF @updatedDefinition = @definition
                    THROW 50008, 'Current faculty with accrual view did not contain the IAM activity projection.', 1;

                SET @updatedDefinition = REPLACE(@updatedDefinition, N'CREATE', N'ALTER');
                EXEC sys.sp_executesql @updatedDefinition;
                """);

            migrationBuilder.Sql(
                """
                DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[usp_PromotePeople]'));
                DECLARE @updatedDefinition nvarchar(max);

                IF @definition IS NULL
                    THROW 50009, 'People promotion procedure was not found.', 1;

                SET @updatedDefinition = REPLACE(
                    @definition,
                    N'[Pronouns], [IsActiveInIam],',
                    N'[Pronouns],');

                IF @updatedDefinition = @definition
                    THROW 50010, 'People promotion procedure did not contain the IAM activity column.', 1;

                SET @updatedDefinition = REPLACE(@updatedDefinition, N'CREATE', N'ALTER');
                EXEC sys.sp_executesql @updatedDefinition;
                """);

            migrationBuilder.DropColumn(
                name: "IsActiveInIam",
                schema: "dbo",
                table: "People_Staging");

            migrationBuilder.DropColumn(
                name: "IsActiveInIam",
                schema: "dbo",
                table: "People");
        }
    }
}
