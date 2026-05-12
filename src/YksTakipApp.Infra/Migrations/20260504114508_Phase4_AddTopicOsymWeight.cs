using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YksTakipApp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_AddTopicOsymWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OsymWeight",
                table: "Topics",
                type: "double",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsymWeight",
                table: "Topics");
        }
    }
}
