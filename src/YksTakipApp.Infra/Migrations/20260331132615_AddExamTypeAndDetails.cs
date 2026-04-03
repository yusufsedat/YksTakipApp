using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddExamTypeAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "ExamResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ExamResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorReasons",
                table: "ExamResults",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExamType",
                table: "ExamResults",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "TYT")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "ExamResults",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExamDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ExamResultId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Wrong = table.Column<int>(type: "int", nullable: false),
                    Blank = table.Column<int>(type: "int", nullable: false),
                    Net = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamDetails_ExamResults_ExamResultId",
                        column: x => x.ExamResultId,
                        principalTable: "ExamResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ExamType",
                table: "ExamResults",
                column: "ExamType");

            migrationBuilder.CreateIndex(
                name: "IX_ExamDetails_ExamResultId",
                table: "ExamDetails",
                column: "ExamResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamDetails");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_ExamType",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "ErrorReasons",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "ExamType",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "ExamResults");
        }
    }
}
