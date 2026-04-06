using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ProblemNoteCloudinaryImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageBase64",
                table: "ProblemNotes",
                newName: "ImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "ImagePublicId",
                table: "ProblemNotes",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePublicId",
                table: "ProblemNotes");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "ProblemNotes",
                newName: "ImageBase64");
        }
    }
}
