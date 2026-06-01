using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordWeightsAndTotalSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, float>>(
                name: "KeywordWeights",
                table: "UserProfiles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalSignals",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeywordWeights",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TotalSignals",
                table: "UserProfiles");
        }
    }
}
