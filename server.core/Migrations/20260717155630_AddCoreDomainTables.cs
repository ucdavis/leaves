using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreDomainTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSetting",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedByAppUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSetting", x => x.SettingKey);
                    table.ForeignKey(
                        name: "FK_AppSetting_AppUser_UpdatedByAppUserId",
                        column: x => x.UpdatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClusterCaoAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClusterId = table.Column<int>(type: "int", nullable: false),
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    EffectiveStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveEndDateExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByAppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedByAppUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterCaoAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterCaoAssignment_AppUser_ClosedByAppUserId",
                        column: x => x.ClosedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClusterCaoAssignment_AppUser_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClusterCaoAssignment_Cluster_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Cluster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentChairAssignment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    EffectiveStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveEndDateExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByAppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedByAppUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentChairAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentChairAssignment_AppUser_ClosedByAppUserId",
                        column: x => x.ClosedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentChairAssignment_AppUser_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentChairAssignment_Department_DepartmentCode",
                        column: x => x.DepartmentCode,
                        principalTable: "Department",
                        principalColumn: "DepartmentCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeReportingDepartmentOverride",
                columns: table => new
                {
                    EmployeeReportingDepartmentOverrideId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    DepartmentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EffectiveStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveEndDateExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByAppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedByAppUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeReportingDepartmentOverride", x => x.EmployeeReportingDepartmentOverrideId);
                    table.ForeignKey(
                        name: "FK_EmployeeReportingDepartmentOverride_AppUser_ClosedByAppUserId",
                        column: x => x.ClosedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReportingDepartmentOverride_AppUser_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReportingDepartmentOverride_Department_DepartmentCode",
                        column: x => x.DepartmentCode,
                        principalTable: "Department",
                        principalColumn: "DepartmentCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestDay",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveRequestId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestDay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequestDay_LeaveRequest_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboundMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveRequestId = table.Column<long>(type: "bigint", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NotBeforeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboundMessage_LeaveRequest_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "People",
                schema: "dbo",
                columns: table => new
                {
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    EmployeeId = table.Column<string>(type: "char(8)", maxLength: 8, nullable: true),
                    StudentId = table.Column<string>(type: "char(9)", maxLength: 9, nullable: true),
                    ExternalId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: true),
                    FirstName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    MiddleName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    LastName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Suffix = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true),
                    FullName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    Pronouns = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_People", x => x.IamId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSetting_UpdatedByAppUserId",
                table: "AppSetting",
                column: "UpdatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaoAssignment_Cluster_EffectiveDates",
                table: "ClusterCaoAssignment",
                columns: new[] { "ClusterId", "EffectiveStartDate", "EffectiveEndDateExclusive" });

            migrationBuilder.CreateIndex(
                name: "IX_CaoAssignment_IamId",
                table: "ClusterCaoAssignment",
                column: "IamId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterCaoAssignment_ClosedByAppUserId",
                table: "ClusterCaoAssignment",
                column: "ClosedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterCaoAssignment_CreatedByAppUserId",
                table: "ClusterCaoAssignment",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChairAssignment_Department_EffectiveDates",
                table: "DepartmentChairAssignment",
                columns: new[] { "DepartmentCode", "EffectiveStartDate", "EffectiveEndDateExclusive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChairAssignment_IamId",
                table: "DepartmentChairAssignment",
                column: "IamId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentChairAssignment_ClosedByAppUserId",
                table: "DepartmentChairAssignment",
                column: "ClosedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentChairAssignment_CreatedByAppUserId",
                table: "DepartmentChairAssignment",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReportingDepartmentOverride_ClosedByAppUserId",
                table: "EmployeeReportingDepartmentOverride",
                column: "ClosedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReportingDepartmentOverride_CreatedByAppUserId",
                table: "EmployeeReportingDepartmentOverride",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingDeptOverride_DepartmentCode",
                table: "EmployeeReportingDepartmentOverride",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingDeptOverride_IamId_EffectiveDates",
                table: "EmployeeReportingDepartmentOverride",
                columns: new[] { "IamId", "EffectiveStartDate", "EffectiveEndDateExclusive" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDay_LeaveDate",
                table: "LeaveRequestDay",
                column: "LeaveDate");

            migrationBuilder.CreateIndex(
                name: "UX_LeaveRequestDay_Request_Date",
                table: "LeaveRequestDay",
                columns: new[] { "LeaveRequestId", "LeaveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_CreatedUtc",
                table: "OutboundMessage",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_LeaveRequestId",
                table: "OutboundMessage",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_NotificationType",
                table: "OutboundMessage",
                column: "NotificationType");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessage_Status_NotBefore_LockedUntil",
                table: "OutboundMessage",
                columns: new[] { "Status", "NotBeforeUtc", "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_OutboundMessage_DedupeKey",
                table: "OutboundMessage",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_EmployeeId",
                schema: "dbo",
                table: "People",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSetting");

            migrationBuilder.DropTable(
                name: "ClusterCaoAssignment");

            migrationBuilder.DropTable(
                name: "DepartmentChairAssignment");

            migrationBuilder.DropTable(
                name: "EmployeeReportingDepartmentOverride");

            migrationBuilder.DropTable(
                name: "LeaveRequestDay");

            migrationBuilder.DropTable(
                name: "OutboundMessage");

            migrationBuilder.DropTable(
                name: "People",
                schema: "dbo");
        }
    }
}
