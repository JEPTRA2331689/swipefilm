using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddServerMovieConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServerMovie_ServerId",
                table: "ServerMovie");

            migrationBuilder.CreateIndex(
                name: "idx_servermovie_server_movie",
                table: "ServerMovie",
                columns: new[] { "ServerId", "MovieId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_servermovie_server_movie",
                table: "ServerMovie");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMovie_ServerId",
                table: "ServerMovie",
                column: "ServerId");
        }
    }
}
