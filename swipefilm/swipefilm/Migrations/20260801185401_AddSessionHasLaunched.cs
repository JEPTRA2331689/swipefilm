using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionHasLaunched : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasLaunched",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasLaunched",
                table: "Sessions");
        }
    }
}
