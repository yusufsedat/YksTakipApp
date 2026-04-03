using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyTimeTopicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "StudyTimes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyTimes_TopicId",
                table: "StudyTimes",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyTimes_Topics_TopicId",
                table: "StudyTimes",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyTimes_Topics_TopicId",
                table: "StudyTimes");

            migrationBuilder.DropIndex(
                name: "IX_StudyTimes_TopicId",
                table: "StudyTimes");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "StudyTimes");
        }
    }
}
