using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "ExamResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyTimes_UserId_Date",
                table: "StudyTimes",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_TopicId",
                table: "ExamResults",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_UserId_TopicId",
                table: "ExamResults",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExamResults_Topics_TopicId",
                table: "ExamResults",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamResults_Topics_TopicId",
                table: "ExamResults");

            migrationBuilder.DropIndex(
                name: "IX_StudyTimes_UserId_Date",
                table: "StudyTimes");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_TopicId",
                table: "ExamResults");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_UserId_TopicId",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "ExamResults");
        }
    }
}
