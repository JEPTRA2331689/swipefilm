using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddServerItemIdAndMachineIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MachineIdentifier",
                table: "UserServers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerItemId",
                table: "ServerSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerItemId",
                table: "ServerMovie",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MachineIdentifier",
                table: "UserServers");

            migrationBuilder.DropColumn(
                name: "ServerItemId",
                table: "ServerSeries");

            migrationBuilder.DropColumn(
                name: "ServerItemId",
                table: "ServerMovie");
        }
    }
}
