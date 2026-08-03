using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentAccrualBalanceView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var viewDefinition =
                """
                CREATE OR ALTER VIEW [dbo].[vw_CurrentAccrualBalance]
                AS
                WITH [LatestEmployeeAccrualDate] AS
                (
                    SELECT
                        [EmployeeId],
                        MAX([AsOfDate]) AS [LatestAsOfDate]
                    FROM [dbo].[EmployeeAccrualBalances]
                    GROUP BY [EmployeeId]
                )
                SELECT
                    [person].[IamId] AS [IamId],
                    [person].[EmployeeId] AS [EmployeeId],
                    [latest].[LatestAsOfDate] AS [LatestAsOfDate],
                    [balance].[LeaveTypeNumber] AS [LeaveTypeNumber],
                    MAX([balance].[TypeLabel]) AS [TypeLabel],
                    MAX([balance].[CalculatedBal]) AS [CalculatedBal],
                    MAX([balance].[AccrualLimit]) AS [AccrualLimit],
                    MAX([balance].[ApproachingMax]) AS [ApproachingMax],
                    MAX([balance].[AccrualPercentage]) AS [AccrualPercentage],
                    COUNT(*) AS [PositionRowCount],
                    MIN([balance].[CalculatedBal]) AS [MinCalculatedBal],
                    MAX([balance].[CalculatedBal]) AS [MaxCalculatedBal],
                    CAST(
                        CASE
                            WHEN MIN([balance].[CalculatedBal]) <> MAX([balance].[CalculatedBal]) THEN 1
                            ELSE 0
                        END
                        AS bit) AS [HasDivergentPositionBalances]
                FROM [dbo].[People] AS [person]
                INNER JOIN [LatestEmployeeAccrualDate] AS [latest]
                    ON [latest].[EmployeeId] = CONVERT(nvarchar(11), [person].[EmployeeId])
                INNER JOIN [dbo].[EmployeeAccrualBalances] AS [balance]
                    ON [balance].[EmployeeId] = [latest].[EmployeeId]
                    AND [balance].[AsOfDate] = [latest].[LatestAsOfDate]
                WHERE [person].[IsEmployee] = CAST(1 AS bit)
                GROUP BY
                    [person].[IamId],
                    [person].[EmployeeId],
                    [latest].[LatestAsOfDate],
                    [balance].[LeaveTypeNumber];
                """;

            // CREATE VIEW must be the first statement in its batch. Executing the definition
            // dynamically keeps it valid in EF's transactional and idempotent migration scripts.
            migrationBuilder.Sql($"EXEC(N'{viewDefinition.Replace("'", "''")}');");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_CurrentAccrualBalance];");
        }
    }
}
