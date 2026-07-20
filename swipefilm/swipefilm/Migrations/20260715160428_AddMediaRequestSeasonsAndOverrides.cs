using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRequestSeasonsAndOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QualityProfileId",
                table: "MediaRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootFolderPath",
                table: "MediaRequests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaRequestSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRequestSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaRequestSeasons_MediaRequests_MediaRequestId",
                        column: x => x.MediaRequestId,
                        principalTable: "MediaRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_mediarequestseasons_requestid_seasonnumber",
                table: "MediaRequestSeasons",
                columns: new[] { "MediaRequestId", "SeasonNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaRequestSeasons");

            migrationBuilder.DropColumn(
                name: "QualityProfileId",
                table: "MediaRequests");

            migrationBuilder.DropColumn(
                name: "RootFolderPath",
                table: "MediaRequests");
        }
    }
}
