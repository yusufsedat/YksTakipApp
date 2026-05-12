using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_AddIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "StudyTimes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "ExamResults",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudyTimes_UserId_ClientRequestId",
                table: "StudyTimes",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_UserId_ClientRequestId",
                table: "ExamResults",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyTimes_UserId_ClientRequestId",
                table: "StudyTimes");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_UserId_ClientRequestId",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "StudyTimes");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ExamResults");
        }
    }
}
