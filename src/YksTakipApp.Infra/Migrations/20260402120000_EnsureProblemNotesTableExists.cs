using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <summary>
    /// Bazı ortamlarda AddProblemNotesTable boş uygulanmış olabilir; tablo yoksa oluşturur.
    /// </summary>
    public partial class EnsureProblemNotesTableExists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `ProblemNotes` (
                  `Id` int NOT NULL AUTO_INCREMENT,
                  `UserId` int NOT NULL,
                  `ImageBase64` longtext NOT NULL,
                  `TagsJson` varchar(4000) NOT NULL,
                  `SolutionLearned` tinyint(1) NOT NULL,
                  `CreatedAt` datetime(6) NOT NULL,
                  PRIMARY KEY (`Id`),
                  KEY `IX_ProblemNotes_CreatedAt` (`CreatedAt`),
                  KEY `IX_ProblemNotes_UserId` (`UserId`),
                  CONSTRAINT `FK_ProblemNotes_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
                ) DEFAULT CHARSET=utf8mb4;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alınmıyor (veri kaybı riski); gerekirse tablo elle silinir.
        }
    }
}
