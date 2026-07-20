using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class MigrateMediaRequestToSeriesPolymorphic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaRequests_Movies_MovieId1",
                table: "MediaRequests");

            migrationBuilder.DropIndex(
                name: "IX_MediaRequests_MovieId1",
                table: "MediaRequests");

            migrationBuilder.RenameColumn(
                name: "MovieId1",
                table: "MediaRequests",
                newName: "SeriesId");

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "MediaRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "idx_requests_seriesid_status",
                table: "MediaRequests",
                columns: new[] { "SeriesId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_mediarequests_movie_xor_series",
                table: "MediaRequests",
                sql: "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaRequests_Series_SeriesId",
                table: "MediaRequests",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaRequests_Series_SeriesId",
                table: "MediaRequests");

            migrationBuilder.DropIndex(
                name: "idx_requests_seriesid_status",
                table: "MediaRequests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mediarequests_movie_xor_series",
                table: "MediaRequests");

            migrationBuilder.RenameColumn(
                name: "SeriesId",
                table: "MediaRequests",
                newName: "MovieId1");

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "MediaRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaRequests_MovieId1",
                table: "MediaRequests",
                column: "MovieId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaRequests_Movies_MovieId1",
                table: "MediaRequests",
                column: "MovieId1",
                principalTable: "Movies",
                principalColumn: "Id");
        }
    }
}
