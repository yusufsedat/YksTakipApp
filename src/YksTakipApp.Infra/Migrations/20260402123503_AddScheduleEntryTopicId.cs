using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleEntryTopicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "ScheduleEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_TopicId",
                table: "ScheduleEntries",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Topics_TopicId",
                table: "ScheduleEntries",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Topics_TopicId",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_TopicId",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "ScheduleEntries");
        }
    }
}
