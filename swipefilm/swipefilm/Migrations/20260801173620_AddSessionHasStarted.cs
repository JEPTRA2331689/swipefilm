using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionHasStarted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasStarted",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasStarted",
                table: "Sessions");
        }
    }
}
