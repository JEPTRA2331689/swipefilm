using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class newRadarrprofile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultQualityProfileId",
                table: "UserSonarr",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQualityProfileName",
                table: "UserSonarr",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRootFolderPath",
                table: "UserSonarr",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultQualityProfileId",
                table: "UserRadarr",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQualityProfileName",
                table: "UserRadarr",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRootFolderPath",
                table: "UserRadarr",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultQualityProfileId",
                table: "UserSonarr");

            migrationBuilder.DropColumn(
                name: "DefaultQualityProfileName",
                table: "UserSonarr");

            migrationBuilder.DropColumn(
                name: "DefaultRootFolderPath",
                table: "UserSonarr");

            migrationBuilder.DropColumn(
                name: "DefaultQualityProfileId",
                table: "UserRadarr");

            migrationBuilder.DropColumn(
                name: "DefaultQualityProfileName",
                table: "UserRadarr");

            migrationBuilder.DropColumn(
                name: "DefaultRootFolderPath",
                table: "UserRadarr");
        }
    }
}
