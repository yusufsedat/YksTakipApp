using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_PlannerAdaptiveLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PriorityExpiresAt",
                table: "UserTopics",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriorityRequestedAt",
                table: "UserTopics",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriorityResolvedAt",
                table: "UserTopics",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTopics_UserId_IsPriorityRequested_PriorityExpiresAt",
                table: "UserTopics",
                columns: new[] { "UserId", "IsPriorityRequested", "PriorityExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTopics_UserId_IsPriorityRequested_PriorityExpiresAt",
                table: "UserTopics");

            migrationBuilder.DropColumn(
                name: "PriorityExpiresAt",
                table: "UserTopics");

            migrationBuilder.DropColumn(
                name: "PriorityRequestedAt",
                table: "UserTopics");

            migrationBuilder.DropColumn(
                name: "PriorityResolvedAt",
                table: "UserTopics");
        }
    }
}
