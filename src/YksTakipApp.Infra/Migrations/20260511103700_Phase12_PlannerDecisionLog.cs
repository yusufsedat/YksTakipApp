using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase12_PlannerDecisionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannerDecisionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<int>(type: "int", nullable: false),
                    TaskCountTotal = table.Column<int>(type: "int", nullable: false),
                    TaskCountStudy = table.Column<int>(type: "int", nullable: false),
                    TaskCountReview = table.Column<int>(type: "int", nullable: false),
                    TaskCountDiagnostic = table.Column<int>(type: "int", nullable: false),
                    PreservedTaskCount = table.Column<int>(type: "int", nullable: false),
                    RecommendationCandidateCount = table.Column<int>(type: "int", nullable: false),
                    RecommendationScheduledCount = table.Column<int>(type: "int", nullable: false),
                    RecommendationSkippedByCapacityCount = table.Column<int>(type: "int", nullable: false),
                    RecommendationSkippedByDuplicateCount = table.Column<int>(type: "int", nullable: false),
                    DailyCapacity = table.Column<int>(type: "int", nullable: false),
                    WorkingDaily = table.Column<int>(type: "int", nullable: false),
                    BufferDaily = table.Column<int>(type: "int", nullable: false),
                    EffectiveCapacityMultiplier = table.Column<double>(type: "double", nullable: false),
                    DynamicBufferRate = table.Column<double>(type: "double", nullable: false),
                    PriorityActiveCount = table.Column<int>(type: "int", nullable: false),
                    PriorityPlacedCount = table.Column<int>(type: "int", nullable: false),
                    InjectedReviewTaskCount = table.Column<int>(type: "int", nullable: false),
                    QualityScore = table.Column<int>(type: "int", nullable: true),
                    QualityBand = table.Column<int>(type: "int", nullable: true),
                    BreakdownJson = table.Column<string>(type: "varchar(8000)", maxLength: 8000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerDecisionLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerDecisionLogs_ReasonCode",
                table: "PlannerDecisionLogs",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerDecisionLogs_UserId_CreatedAt",
                table: "PlannerDecisionLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerDecisionLogs_WeekStart",
                table: "PlannerDecisionLogs",
                column: "WeekStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannerDecisionLogs");
        }
    }
}
