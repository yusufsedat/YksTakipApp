using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_AddUserGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TargetUniversity = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetDepartment = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetTytNet = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    TargetAytNet = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGoals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserGoals_UserId_CreatedAt",
                table: "UserGoals",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveGoalVersionId",
                table: "Users",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ActiveGoalVersionId",
                table: "Users",
                column: "ActiveGoalVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_UserGoals_ActiveGoalVersionId",
                table: "Users",
                column: "ActiveGoalVersionId",
                principalTable: "UserGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddColumn<int>(
                name: "SmartOnboardingSkipCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_UserGoals_ActiveGoalVersionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ActiveGoalVersionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActiveGoalVersionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SmartOnboardingSkipCount",
                table: "Users");

            migrationBuilder.DropTable(
                name: "UserGoals");
        }
    }
}
