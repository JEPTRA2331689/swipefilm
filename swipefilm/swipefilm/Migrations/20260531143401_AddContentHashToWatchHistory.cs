using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddContentHashToWatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "WatchHistory",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "WatchHistory");
        }
    }
}
