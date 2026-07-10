using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class TablesForAdminPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cluster",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClusterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByAppUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cluster", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cluster_AppUser_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeaveType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveTypeKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceLeaveTypeNumber = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HasAccrualBalance = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Department",
                columns: table => new
                {
                    DepartmentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceLevel = table.Column<byte>(type: "tinyint", nullable: true),
                    ClusterId = table.Column<int>(type: "int", nullable: true),
                    WorkflowMode = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSeenInSourceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.DepartmentCode);
                    table.ForeignKey(
                        name: "FK_Department_Cluster_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Cluster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequest",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    EmployeeId = table.Column<string>(type: "char(8)", maxLength: 8, nullable: true),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    PayLeaveTypeId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoveragePlan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReportingDepartmentCodeSnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReportingDepartmentNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClusterIdSnapshot = table.Column<int>(type: "int", nullable: true),
                    WorkflowModeSnapshot = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequest_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequest_LeaveType_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequest_LeaveType_PayLeaveTypeId",
                        column: x => x.PayLeaveTypeId,
                        principalTable: "LeaveType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentEmailRouting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedByAppUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentEmailRouting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentEmailRouting_AppUser_UpdatedByAppUserId",
                        column: x => x.UpdatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentEmailRouting_Department_DepartmentCode",
                        column: x => x.DepartmentCode,
                        principalTable: "Department",
                        principalColumn: "DepartmentCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestAction",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActorAppUserId = table.Column<int>(type: "int", nullable: true),
                    ActorIamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsSelfAction = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequestAction_AppUser_ActorAppUserId",
                        column: x => x.ActorAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeaveRequestAction_LeaveRequest_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cluster_CreatedByAppUserId",
                table: "Cluster",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Department_ClusterId",
                table: "Department",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Department_LastSeenInSourceAt",
                table: "Department",
                column: "LastSeenInSourceAt");

            migrationBuilder.CreateIndex(
                name: "IX_Department_WorkflowMode",
                table: "Department",
                column: "WorkflowMode");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentEmailRouting_DepartmentCode",
                table: "DepartmentEmailRouting",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentEmailRouting_UpdatedByAppUserId",
                table: "DepartmentEmailRouting",
                column: "UpdatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_AppUserId",
                table: "LeaveRequest",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_DateRange",
                table: "LeaveRequest",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_Department_Status",
                table: "LeaveRequest",
                columns: new[] { "ReportingDepartmentCodeSnapshot", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_IamId",
                table: "LeaveRequest",
                column: "IamId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_LeaveTypeId",
                table: "LeaveRequest",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_PayLeaveTypeId",
                table: "LeaveRequest",
                column: "PayLeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequest_Status",
                table: "LeaveRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestAction_ActionAt",
                table: "LeaveRequestAction",
                column: "ActionAt");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestAction_ActorAppUserId",
                table: "LeaveRequestAction",
                column: "ActorAppUserId");

            migrationBuilder.CreateIndex(
                name: "UX_LeaveRequestAction_LeaveRequestId",
                table: "LeaveRequestAction",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveType_SourceLeaveTypeNumber",
                table: "LeaveType",
                column: "SourceLeaveTypeNumber");

            migrationBuilder.CreateIndex(
                name: "UX_LeaveType_LeaveTypeKey",
                table: "LeaveType",
                column: "LeaveTypeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentEmailRouting");

            migrationBuilder.DropTable(
                name: "LeaveRequestAction");

            migrationBuilder.DropTable(
                name: "Department");

            migrationBuilder.DropTable(
                name: "LeaveRequest");

            migrationBuilder.DropTable(
                name: "Cluster");

            migrationBuilder.DropTable(
                name: "LeaveType");
        }
    }
}
