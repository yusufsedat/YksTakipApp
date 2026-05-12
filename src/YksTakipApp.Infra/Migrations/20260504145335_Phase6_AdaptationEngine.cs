using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations;

/// <inheritdoc />
public partial class Phase6_AdaptationEngine : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsLocked",
            table: "UserTopics",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastEvaluatedAt",
            table: "UserTopics",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "MasteryConfidence",
            table: "UserTopics",
            type: "double",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<string>(
                name: "MasteryStatus",
                table: "UserTopics",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotStarted")
            .Annotation("MySql:CharSet", "utf8mb4");

        // ScheduleTasks: Guid PK -> int identity (EF AlterColumn is unsafe for MySQL; use raw SQL)
        migrationBuilder.Sql(
            """
            ALTER TABLE `ScheduleTasks` DROP PRIMARY KEY;
            ALTER TABLE `ScheduleTasks` CHANGE COLUMN `Id` `LegacyId` CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `Id` INT NOT NULL AUTO_INCREMENT,
                ADD PRIMARY KEY (`Id`);
            ALTER TABLE `ScheduleTasks` DROP COLUMN `LegacyId`;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `MainTopicId` int NULL;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `PrerequisiteTopicId` int NULL;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `Reason` varchar(500) CHARACTER SET utf8mb4 NULL;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `TaskType` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Study';
            """);

        migrationBuilder.CreateTable(
                name: "TopicPrerequisites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteTopicId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicPrerequisites", x => x.Id);
                    table.CheckConstraint("CK_TopicPrerequisite_NoSelf", "`TopicId` <> `PrerequisiteTopicId`");
                    table.ForeignKey(
                        name: "FK_TopicPrerequisites_Topics_PrerequisiteTopicId",
                        column: x => x.PrerequisiteTopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TopicPrerequisites_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_UserTopics_UserId_IsLocked",
            table: "UserTopics",
            columns: new[] { "UserId", "IsLocked" });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleTasks_UserId_TaskDate_TaskType",
            table: "ScheduleTasks",
            columns: new[] { "UserId", "TaskDate", "TaskType" });

        migrationBuilder.CreateIndex(
            name: "IX_TopicPrerequisites_PrerequisiteTopicId",
            table: "TopicPrerequisites",
            column: "PrerequisiteTopicId");

        migrationBuilder.CreateIndex(
            name: "IX_TopicPrerequisites_TopicId_PrerequisiteTopicId",
            table: "TopicPrerequisites",
            columns: new[] { "TopicId", "PrerequisiteTopicId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TopicPrerequisites");

        migrationBuilder.DropIndex(
            name: "IX_UserTopics_UserId_IsLocked",
            table: "UserTopics");

        migrationBuilder.DropIndex(
            name: "IX_ScheduleTasks_UserId_TaskDate_TaskType",
            table: "ScheduleTasks");

        migrationBuilder.DropColumn(
            name: "IsLocked",
            table: "UserTopics");

        migrationBuilder.DropColumn(
            name: "LastEvaluatedAt",
            table: "UserTopics");

        migrationBuilder.DropColumn(
            name: "MasteryConfidence",
            table: "UserTopics");

        migrationBuilder.DropColumn(
            name: "MasteryStatus",
            table: "UserTopics");

        // Restore Guid PK (destructive: new UUIDs; dev rollback only)
        migrationBuilder.Sql(
            """
            ALTER TABLE `ScheduleTasks` DROP COLUMN `TaskType`;
            ALTER TABLE `ScheduleTasks` DROP COLUMN `Reason`;
            ALTER TABLE `ScheduleTasks` DROP COLUMN `PrerequisiteTopicId`;
            ALTER TABLE `ScheduleTasks` DROP COLUMN `MainTopicId`;
            ALTER TABLE `ScheduleTasks` DROP PRIMARY KEY;
            ALTER TABLE `ScheduleTasks` ADD COLUMN `LegacyId` CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL;
            UPDATE `ScheduleTasks` SET `LegacyId` = UUID();
            ALTER TABLE `ScheduleTasks` DROP COLUMN `Id`;
            ALTER TABLE `ScheduleTasks` CHANGE COLUMN `LegacyId` `Id` CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL;
            ALTER TABLE `ScheduleTasks` ADD PRIMARY KEY (`Id`);
            """);
    }
}
