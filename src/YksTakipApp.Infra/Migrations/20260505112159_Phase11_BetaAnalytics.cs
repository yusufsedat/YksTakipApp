using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase11_BetaAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPlannerChurnEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TriggerDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false),
                    DaysSinceLastCompletedTask = table.Column<int>(type: "int", nullable: true),
                    DaysSincePlanGenerated = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlannerChurnEvents", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlannerChurnEvents_TriggerDate_ReasonCode",
                table: "UserPlannerChurnEvents",
                columns: new[] { "TriggerDate", "ReasonCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlannerChurnEvents_UserId_TriggerDate",
                table: "UserPlannerChurnEvents",
                columns: new[] { "UserId", "TriggerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlannerChurnEvents_UserId_WeekStart_ReasonCode",
                table: "UserPlannerChurnEvents",
                columns: new[] { "UserId", "WeekStart", "ReasonCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPlannerChurnEvents");
        }
    }
}
