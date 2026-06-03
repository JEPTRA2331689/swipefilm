using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddServerMovie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerMovie_UserProfiles_ServerId",
                table: "ServerMovie");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerMovie_UserServers_ServerId",
                table: "ServerMovie",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServerMovie_UserServers_ServerId",
                table: "ServerMovie");

            migrationBuilder.AddForeignKey(
                name: "FK_ServerMovie_UserProfiles_ServerId",
                table: "ServerMovie",
                column: "ServerId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
