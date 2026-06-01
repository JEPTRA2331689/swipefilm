using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserServers_UserId",
                table: "UserServers");

            migrationBuilder.DropIndex(
                name: "IX_Requests_UserId",
                table: "Requests");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistory_UserId",
                table: "WatchHistory",
                newName: "idx_watchhistory_userid");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistory_ServerId",
                table: "WatchHistory",
                newName: "idx_watchhistory_serverid");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistory_MovieId",
                table: "WatchHistory",
                newName: "idx_watchhistory_movieid");

            migrationBuilder.RenameIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                newName: "idx_userprofiles_userid");

            migrationBuilder.RenameIndex(
                name: "IX_Swipes_UserId",
                table: "Swipes",
                newName: "idx_swipes_userid");

            migrationBuilder.RenameIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies",
                newName: "idx_movies_tmdbid");

            migrationBuilder.RenameIndex(
                name: "IX_Movies_ContentType",
                table: "Movies",
                newName: "idx_movies_contenttype");

            migrationBuilder.RenameIndex(
                name: "IX_Movies_CachedAt",
                table: "Movies",
                newName: "idx_movies_cachedat");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Movies",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "idx_watchhistory_userid_isfavorite",
                table: "WatchHistory",
                columns: new[] { "UserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "idx_watchhistory_userid_serverid",
                table: "WatchHistory",
                columns: new[] { "UserId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "idx_userservers_userid_isactive",
                table: "UserServers",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "idx_swipes_userid_createdat",
                table: "Swipes",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_swipes_userid_direction",
                table: "Swipes",
                columns: new[] { "UserId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "idx_requests_userid_status",
                table: "Requests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "idx_movies_rating_popularity",
                table: "Movies",
                columns: new[] { "TmdbRating", "TmdbPopularity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_watchhistory_userid_isfavorite",
                table: "WatchHistory");

            migrationBuilder.DropIndex(
                name: "idx_watchhistory_userid_serverid",
                table: "WatchHistory");

            migrationBuilder.DropIndex(
                name: "idx_userservers_userid_isactive",
                table: "UserServers");

            migrationBuilder.DropIndex(
                name: "idx_swipes_userid_createdat",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "idx_swipes_userid_direction",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "idx_requests_userid_status",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "idx_movies_rating_popularity",
                table: "Movies");

            migrationBuilder.RenameIndex(
                name: "idx_watchhistory_userid",
                table: "WatchHistory",
                newName: "IX_WatchHistory_UserId");

            migrationBuilder.RenameIndex(
                name: "idx_watchhistory_serverid",
                table: "WatchHistory",
                newName: "IX_WatchHistory_ServerId");

            migrationBuilder.RenameIndex(
                name: "idx_watchhistory_movieid",
                table: "WatchHistory",
                newName: "IX_WatchHistory_MovieId");

            migrationBuilder.RenameIndex(
                name: "idx_userprofiles_userid",
                table: "UserProfiles",
                newName: "IX_UserProfiles_UserId");

            migrationBuilder.RenameIndex(
                name: "idx_swipes_userid",
                table: "Swipes",
                newName: "IX_Swipes_UserId");

            migrationBuilder.RenameIndex(
                name: "idx_movies_tmdbid",
                table: "Movies",
                newName: "IX_Movies_TmdbId");

            migrationBuilder.RenameIndex(
                name: "idx_movies_contenttype",
                table: "Movies",
                newName: "IX_Movies_ContentType");

            migrationBuilder.RenameIndex(
                name: "idx_movies_cachedat",
                table: "Movies",
                newName: "IX_Movies_CachedAt");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Movies",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_UserServers_UserId",
                table: "UserServers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_UserId",
                table: "Requests",
                column: "UserId");
        }
    }
}
