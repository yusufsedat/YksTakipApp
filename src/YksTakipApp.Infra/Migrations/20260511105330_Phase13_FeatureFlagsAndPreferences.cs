using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase13_FeatureFlagsAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RolloutPercentage = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    Segment = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserFeatureFlagOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FlagKey = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeatureFlagOverrides", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DailyReminderEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    RecoveryReminderEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    WeeklyReviewEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    QuietHoursStart = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.UserId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Segment",
                table: "FeatureFlags",
                column: "Segment");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeatureFlagOverrides_FlagKey",
                table: "UserFeatureFlagOverrides",
                column: "FlagKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserFeatureFlagOverrides_UserId_FlagKey",
                table: "UserFeatureFlagOverrides",
                columns: new[] { "UserId", "FlagKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "UserFeatureFlagOverrides");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");
        }
    }
}
