using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentEmployeeView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var viewDefinition =
                """
                CREATE OR ALTER VIEW [dbo].[vw_CurrentEmployee]
                AS
                WITH [LatestEmployeeAccrualDate] AS
                (
                    SELECT
                        [EmployeeId],
                        MAX([AsOfDate]) AS [LatestAsOfDate]
                    FROM [dbo].[EmployeeAccrualBalances]
                    GROUP BY [EmployeeId]
                ),
                [CurrentPosition] AS
                (
                    SELECT
                        [balance].[EmployeeId],
                        [balance].[AsOfDate],
                        [balance].[PositionNumber],
                        MAX([balance].[EmployeeEmail]) AS [EmployeeEmail],
                        MAX([balance].[EmployeeName]) AS [EmployeeName],
                        MAX([balance].[HrStatus]) AS [HrStatus],
                        MAX([balance].[EmployeeClassCode]) AS [EmployeeClassCode],
                        MAX([balance].[EmployeeClassDescription]) AS [EmployeeClassDescription],
                        MAX([balance].[JobCode]) AS [JobCode],
                        MAX([balance].[JobCodeDescription]) AS [JobCodeDescription],
                        MAX([balance].[Level3Dept]) AS [Level3Dept],
                        MAX([balance].[Level3DeptDesc]) AS [Level3DeptDesc],
                        MAX([balance].[Level4Dept]) AS [Level4Dept],
                        MAX([balance].[Level4DeptDesc]) AS [Level4DeptDesc],
                        MAX([balance].[Level5Dept]) AS [Level5Dept],
                        MAX([balance].[Level5DeptDesc]) AS [Level5DeptDesc]
                    FROM [dbo].[EmployeeAccrualBalances] AS [balance]
                    INNER JOIN [LatestEmployeeAccrualDate] AS [latest]
                        ON [latest].[EmployeeId] = [balance].[EmployeeId]
                        AND [latest].[LatestAsOfDate] = [balance].[AsOfDate]
                    GROUP BY
                        [balance].[EmployeeId],
                        [balance].[AsOfDate],
                        [balance].[PositionNumber]
                ),
                [RankedCurrentPosition] AS
                (
                    SELECT
                        [position].*,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY [position].[EmployeeId]
                            ORDER BY
                                CASE
                                    WHEN NULLIF(LTRIM(RTRIM([position].[JobCode])), N'') IS NULL THEN 1
                                    ELSE 0
                                END,
                                [position].[JobCode],
                                CASE
                                    WHEN NULLIF(LTRIM(RTRIM([position].[EmployeeClassCode])), N'') IS NULL THEN 1
                                    ELSE 0
                                END,
                                [position].[EmployeeClassCode],
                                [position].[PositionNumber]
                        ) AS [PositionRank]
                    FROM [CurrentPosition] AS [position]
                ),
                [PacificBusinessDate] AS
                (
                    SELECT CAST(
                        SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time'
                        AS date) AS [BusinessDate]
                )
                SELECT
                    [person].[IamId] AS [IamId],
                    [person].[EmployeeId] AS [EmployeeId],
                    CAST(
                        COALESCE(
                            NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(200), [person].[FullName]))), N''),
                            NULLIF(LTRIM(RTRIM([position].[EmployeeName])), N''))
                        AS nvarchar(200)) AS [DisplayName],
                    CAST(
                        COALESCE(
                            NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(320), [person].[Email]))), N''),
                            NULLIF(LTRIM(RTRIM([position].[EmployeeEmail])), N''))
                        AS nvarchar(320)) AS [Email],
                    [latest].[LatestAsOfDate] AS [LatestAsOfDate],
                    CAST(CASE WHEN [latest].[EmployeeId] IS NULL THEN 0 ELSE 1 END AS bit)
                        AS [HasCurrentAccrualRecord],
                    [position].[HrStatus] AS [HrStatus],
                    [position].[EmployeeClassCode] AS [EmployeeClassCode],
                    [position].[EmployeeClassDescription] AS [EmployeeClassDescription],
                    [position].[JobCode] AS [JobCode],
                    [position].[JobCodeDescription] AS [JobCodeDescription],
                    [sourceDepartment].[DepartmentCode] AS [SourceDepartmentCode],
                    [sourceDepartment].[DepartmentName] AS [SourceDepartmentName],
                    COALESCE([activeOverride].[DepartmentCode], [sourceDepartment].[DepartmentCode])
                        AS [ResolvedReportingDepartmentCode],
                    COALESCE([activeOverride].[DepartmentName], [sourceDepartment].[DepartmentName])
                        AS [ResolvedReportingDepartmentName],
                    [activeOverride].[EmployeeReportingDepartmentOverrideId]
                        AS [ReportingDepartmentOverrideId],
                    CAST(
                        CASE
                            WHEN [activeOverride].[EmployeeReportingDepartmentOverrideId] IS NULL THEN 0
                            ELSE 1
                        END
                        AS bit) AS [HasReportingDepartmentOverride]
                FROM [dbo].[People] AS [person]
                CROSS JOIN [PacificBusinessDate] AS [pacificDate]
                LEFT JOIN [LatestEmployeeAccrualDate] AS [latest]
                    ON [latest].[EmployeeId] = [person].[EmployeeId]
                LEFT JOIN [RankedCurrentPosition] AS [position]
                    ON [position].[EmployeeId] = [person].[EmployeeId]
                    AND [position].[PositionRank] = 1
                OUTER APPLY
                (
                    SELECT TOP (1)
                        [departmentChoice].[DepartmentCode],
                        [departmentChoice].[DepartmentName]
                    FROM
                    (
                        VALUES
                            (1, NULLIF(LTRIM(RTRIM([position].[Level5Dept])), N''), [position].[Level5DeptDesc]),
                            (2, NULLIF(LTRIM(RTRIM([position].[Level4Dept])), N''), [position].[Level4DeptDesc]),
                            (3, NULLIF(LTRIM(RTRIM([position].[Level3Dept])), N''), [position].[Level3DeptDesc])
                    ) AS [departmentChoice] ([Preference], [DepartmentCode], [DepartmentName])
                    WHERE [departmentChoice].[DepartmentCode] IS NOT NULL
                    ORDER BY [departmentChoice].[Preference]
                ) AS [sourceDepartment]
                OUTER APPLY
                (
                    SELECT TOP (1)
                        [override].[EmployeeReportingDepartmentOverrideId],
                        [override].[DepartmentCode],
                        [department].[DepartmentName]
                    FROM [dbo].[EmployeeReportingDepartmentOverride] AS [override]
                    INNER JOIN [dbo].[Department] AS [department]
                        ON [department].[DepartmentCode] = [override].[DepartmentCode]
                    WHERE [override].[IamId] = [person].[IamId]
                        AND [override].[EffectiveStartDate] <= [pacificDate].[BusinessDate]
                        AND
                        (
                            [override].[EffectiveEndDateExclusive] IS NULL
                            OR [pacificDate].[BusinessDate] < [override].[EffectiveEndDateExclusive]
                        )
                    ORDER BY
                        [override].[EffectiveStartDate] DESC,
                        [override].[EmployeeReportingDepartmentOverrideId] DESC
                ) AS [activeOverride]
                WHERE [person].[IsEmployee] = CAST(1 AS bit);
                """;

            // CREATE VIEW must be the first statement in its batch. Executing the definition
            // dynamically keeps it valid in EF's transactional and idempotent migration scripts.
            migrationBuilder.Sql($"EXEC(N'{viewDefinition.Replace("'", "''")}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_CurrentEmployee];");
        }
    }
}
