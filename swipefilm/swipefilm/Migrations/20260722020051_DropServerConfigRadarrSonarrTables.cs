using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class DropServerConfigRadarrSonarrTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerConfig");

            migrationBuilder.DropTable(
                name: "UserRadarr");

            migrationBuilder.DropTable(
                name: "UserSonarr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FriendlyName = table.Column<string>(type: "text", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MachineIdentifier = table.Column<string>(type: "text", nullable: true),
                    TokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    UrlEncrypted = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRadarr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefaultQualityProfileId = table.Column<int>(type: "integer", nullable: true),
                    DefaultQualityProfileName = table.Column<string>(type: "text", nullable: true),
                    DefaultRootFolderPath = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UrlEncrypted = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRadarr", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRadarr_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSonarr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefaultQualityProfileId = table.Column<int>(type: "integer", nullable: true),
                    DefaultQualityProfileName = table.Column<string>(type: "text", nullable: true),
                    DefaultRootFolderPath = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UrlEncrypted = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSonarr", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSonarr_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRadarr_UserId",
                table: "UserRadarr",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSonarr_UserId",
                table: "UserSonarr",
                column: "UserId");
        }
    }
}
