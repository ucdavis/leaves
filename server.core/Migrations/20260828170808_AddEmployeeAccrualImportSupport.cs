using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAccrualImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAccrualBalances_Staging",
                schema: "dbo",
                columns: table => new
                {
                    EmployeeId = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PositionNumber = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    LeaveTypeNumber = table.Column<int>(type: "int", nullable: false),
                    EmployeeEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmployeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnionCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UnionDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeClassCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EmployeeClassDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JobCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    JobCodeDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportsToPositionNumber = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ReportsToEmployeeId = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    ReportsToEmployeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HrStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeStatusDescription = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeTypeDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HourlyRateFTE = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    TypeLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrevBal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HoursTaken = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AccrualHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AdjustedHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CalculatedBal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AccrualLimit = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ApproachingMax = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HoursOverUnderPolicyMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AccrualPercentage = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    ExceptionalMaxVacationOnly = table.Column<int>(type: "int", nullable: false),
                    Level1Dept = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level1DeptDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level2Dept = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level2DeptDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level3Dept = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level3DeptDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level4Dept = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level4DeptDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level5Dept = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Level5DeptDesc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoadDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_EmployeeAccrualBalances_Staging",
                        x => new { x.EmployeeId, x.AsOfDate, x.PositionNumber, x.LeaveTypeNumber });
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAccrualBalances_Staging_AsOf_Employee_LeaveType",
                schema: "dbo",
                table: "EmployeeAccrualBalances_Staging",
                columns: new[] { "AsOfDate", "EmployeeId", "LeaveTypeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAccrualBalances_Staging_EmployeeId",
                schema: "dbo",
                table: "EmployeeAccrualBalances_Staging",
                column: "EmployeeId");

            migrationBuilder.Sql(
                """
                EXEC(N'
                CREATE OR ALTER PROCEDURE [dbo].[usp_PromoteEmployeeAccrualBalances]
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
                        FROM [dbo].[EmployeeAccrualBalances_Staging] WITH (TABLOCKX, HOLDLOCK);

                        IF @StagingRowCount = 0
                            THROW 50011, ''Employee accrual balance staging is empty; promotion was not run.'', 1;

                        TRUNCATE TABLE [dbo].[EmployeeAccrualBalances];

                        INSERT INTO [dbo].[EmployeeAccrualBalances]
                        (
                            [EmployeeId],
                            [AsOfDate],
                            [PositionNumber],
                            [LeaveTypeNumber],
                            [EmployeeEmail],
                            [EmployeeName],
                            [UnionCode],
                            [UnionDescription],
                            [EmployeeClassCode],
                            [EmployeeClassDescription],
                            [JobCode],
                            [JobCodeDescription],
                            [ReportsToPositionNumber],
                            [ReportsToEmployeeId],
                            [ReportsToEmployeeName],
                            [HrStatus],
                            [EmployeeStatus],
                            [EmployeeStatusDescription],
                            [EmployeeType],
                            [EmployeeTypeDescription],
                            [HourlyRateFTE],
                            [TypeLabel],
                            [PrevBal],
                            [HoursTaken],
                            [AccrualHours],
                            [AdjustedHours],
                            [CalculatedBal],
                            [AccrualLimit],
                            [ApproachingMax],
                            [HoursOverUnderPolicyMax],
                            [AccrualPercentage],
                            [ExceptionalMaxVacationOnly],
                            [Level1Dept],
                            [Level1DeptDesc],
                            [Level2Dept],
                            [Level2DeptDesc],
                            [Level3Dept],
                            [Level3DeptDesc],
                            [Level4Dept],
                            [Level4DeptDesc],
                            [Level5Dept],
                            [Level5DeptDesc],
                            [LoadDate],
                            [LastUpdated]
                        )
                        SELECT
                            [EmployeeId],
                            [AsOfDate],
                            [PositionNumber],
                            [LeaveTypeNumber],
                            [EmployeeEmail],
                            [EmployeeName],
                            [UnionCode],
                            [UnionDescription],
                            [EmployeeClassCode],
                            [EmployeeClassDescription],
                            [JobCode],
                            [JobCodeDescription],
                            [ReportsToPositionNumber],
                            [ReportsToEmployeeId],
                            [ReportsToEmployeeName],
                            [HrStatus],
                            [EmployeeStatus],
                            [EmployeeStatusDescription],
                            [EmployeeType],
                            [EmployeeTypeDescription],
                            [HourlyRateFTE],
                            [TypeLabel],
                            [PrevBal],
                            [HoursTaken],
                            [AccrualHours],
                            [AdjustedHours],
                            [CalculatedBal],
                            [AccrualLimit],
                            [ApproachingMax],
                            [HoursOverUnderPolicyMax],
                            [AccrualPercentage],
                            [ExceptionalMaxVacationOnly],
                            [Level1Dept],
                            [Level1DeptDesc],
                            [Level2Dept],
                            [Level2DeptDesc],
                            [Level3Dept],
                            [Level3DeptDesc],
                            [Level4Dept],
                            [Level4DeptDesc],
                            [Level5Dept],
                            [Level5DeptDesc],
                            [LoadDate],
                            [LastUpdated]
                        FROM [dbo].[EmployeeAccrualBalances_Staging];

                        SET @InsertedRowCount = @@ROWCOUNT;

                        IF @InsertedRowCount <> @StagingRowCount
                            THROW 50012, ''Employee accrual balance promotion row count did not match staging.'', 1;

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
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[usp_PromoteEmployeeAccrualBalances];");

            migrationBuilder.DropTable(
                name: "EmployeeAccrualBalances_Staging",
                schema: "dbo");
        }
    }
}
