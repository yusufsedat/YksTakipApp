using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <summary>
    /// Bazı ortamlarda AddStudyTimeTopicId boş uygulanmış olabilir; şema ile uyumu garanti eder.
    /// </summary>
    public partial class EnsureStudyTimesTopicId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET @col_exists = (
                  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'StudyTimes'
                    AND COLUMN_NAME = 'TopicId');
                SET @sql = IF(@col_exists = 0,
                  'ALTER TABLE `StudyTimes` ADD COLUMN `TopicId` int NULL',
                  'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                SET @idx_exists = (
                  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'StudyTimes'
                    AND INDEX_NAME = 'IX_StudyTimes_TopicId');
                SET @sqlidx = IF(@idx_exists = 0,
                  'CREATE INDEX `IX_StudyTimes_TopicId` ON `StudyTimes` (`TopicId`)',
                  'SELECT 1');
                PREPARE stmtidx FROM @sqlidx;
                EXECUTE stmtidx;
                DEALLOCATE PREPARE stmtidx;

                SET @fk_exists = (
                  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                  WHERE CONSTRAINT_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'StudyTimes'
                    AND CONSTRAINT_NAME = 'FK_StudyTimes_Topics_TopicId');
                SET @sqlfk = IF(@fk_exists = 0,
                  'ALTER TABLE `StudyTimes` ADD CONSTRAINT `FK_StudyTimes_Topics_TopicId` FOREIGN KEY (`TopicId`) REFERENCES `Topics` (`Id`) ON DELETE SET NULL',
                  'SELECT 1');
                PREPARE stmtfk FROM @sqlfk;
                EXECUTE stmtfk;
                DEALLOCATE PREPARE stmtfk;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: önceki migration ile tutarlı kalsın diye boş bırakıldı (prod veri kaybı riski).
        }
    }
}
