using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class Cleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequestAction_AppUser_ActorAppUserId",
                table: "LeaveRequestAction");

            migrationBuilder.AlterColumn<string>(
                name: "ActorIamId",
                table: "LeaveRequestAction",
                type: "char(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "char(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActorAppUserId",
                table: "LeaveRequestAction",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_DepartmentEmailRouting_DepartmentCode_ToEmail",
                table: "DepartmentEmailRouting",
                columns: new[] { "DepartmentCode", "ToEmail" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequestAction_AppUser_ActorAppUserId",
                table: "LeaveRequestAction",
                column: "ActorAppUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequestAction_AppUser_ActorAppUserId",
                table: "LeaveRequestAction");

            migrationBuilder.DropIndex(
                name: "UX_DepartmentEmailRouting_DepartmentCode_ToEmail",
                table: "DepartmentEmailRouting");

            migrationBuilder.AlterColumn<string>(
                name: "ActorIamId",
                table: "LeaveRequestAction",
                type: "char(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "ActorAppUserId",
                table: "LeaveRequestAction",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequestAction_AppUser_ActorAppUserId",
                table: "LeaveRequestAction",
                column: "ActorAppUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
