using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.core.Migrations
{
    /// <inheritdoc />
    public partial class AppAdminAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdminAssignment",
                columns: table => new
                {
                    AppAdminAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IamId = table.Column<string>(type: "char(10)", maxLength: 10, nullable: false),
                    CreatedByAppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdminAssignment", x => x.AppAdminAssignmentId);
                    table.ForeignKey(
                        name: "FK_AppAdminAssignment_AppUser_CreatedByAppUserId",
                        column: x => x.CreatedByAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdminAssignment_CreatedByAppUserId",
                table: "AppAdminAssignment",
                column: "CreatedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "UX_AppAdminAssignment_IamId",
                table: "AppAdminAssignment",
                column: "IamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdminAssignment");
        }
    }
}
