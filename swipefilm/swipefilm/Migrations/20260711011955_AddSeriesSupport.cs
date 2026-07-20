using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Watchlists_UserId",
                table: "Watchlists",
                newName: "idx_watchlists_userid");

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "Watchlists",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Watchlists",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "Swipes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Swipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TmdbId = table.Column<int>(type: "integer", nullable: false),
                    TvdbId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OriginalTitle = table.Column<string>(type: "text", nullable: true),
                    OriginalLanguage = table.Column<string>(type: "text", nullable: true),
                    PosterPath = table.Column<string>(type: "text", nullable: true),
                    BackdropPath = table.Column<string>(type: "text", nullable: true),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    FirstAirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TmdbRating = table.Column<float>(type: "real", nullable: false),
                    TmdbPopularity = table.Column<float>(type: "real", nullable: false),
                    TmdbVoteCount = table.Column<int>(type: "integer", nullable: false),
                    Genres = table.Column<string[]>(type: "text[]", nullable: false),
                    Keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedBy = table.Column<string[]>(type: "text[]", nullable: false),
                    CastTop5 = table.Column<string[]>(type: "text[]", nullable: false),
                    NumberOfSeasons = table.Column<int>(type: "integer", nullable: false),
                    NumberOfEpisodes = table.Column<int>(type: "integer", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSeriesProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenreWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    CreatorWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    ActorWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    KeywordWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    OriginalLanguageWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    PreferredDecadeWeights = table.Column<Dictionary<string, float>>(type: "jsonb", nullable: false),
                    GenreCounts = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    CreatorCounts = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    ActorCounts = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    KeywordCounts = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    LanguageCounts = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    AvgSeasonCompletionRate = table.Column<float>(type: "real", nullable: false),
                    AvgSeasonsWatchedRatio = table.Column<float>(type: "real", nullable: false),
                    TotalSeriesSignals = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSeriesProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSeriesProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    EpisodeCount = table.Column<int>(type: "integer", nullable: false),
                    TmdbSeasonId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesSeasons_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerSeries_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerSeries_UserServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "UserServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesWatchHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedEpisodeCount = table.Column<int>(type: "integer", nullable: false),
                    UserRating = table.Column<int>(type: "integer", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    FirstWatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastWatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesWatchHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesWatchHistory_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesWatchHistory_SeriesSeasons_SeriesSeasonId",
                        column: x => x.SeriesSeasonId,
                        principalTable: "SeriesSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesWatchHistory_UserServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "UserServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerSeriesSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesSeasonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSeriesSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerSeriesSeasons_SeriesSeasons_SeriesSeasonId",
                        column: x => x.SeriesSeasonId,
                        principalTable: "SeriesSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerSeriesSeasons_UserServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "UserServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_SeriesId",
                table: "Watchlists",
                column: "SeriesId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_watchlists_movie_xor_series",
                table: "Watchlists",
                sql: "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Swipes_SeriesId",
                table: "Swipes",
                column: "SeriesId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_swipes_movie_xor_series",
                table: "Swipes",
                sql: "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_series_cachedat",
                table: "Series",
                column: "CachedAt");

            migrationBuilder.CreateIndex(
                name: "idx_series_rating_popularity",
                table: "Series",
                columns: new[] { "TmdbRating", "TmdbPopularity" });

            migrationBuilder.CreateIndex(
                name: "idx_series_tmdbid",
                table: "Series",
                column: "TmdbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_seriesseasons_seriesid_seasonnumber",
                table: "SeriesSeasons",
                columns: new[] { "SeriesId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_seriesswatchhistory_userid",
                table: "SeriesWatchHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "idx_serieswatchhistory_userid_isfavorite",
                table: "SeriesWatchHistory",
                columns: new[] { "UserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "idx_serieswatchhistory_userid_seasonid",
                table: "SeriesWatchHistory",
                columns: new[] { "UserId", "SeriesSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_serieswatchhistory_userid_serverid",
                table: "SeriesWatchHistory",
                columns: new[] { "UserId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesWatchHistory_SeriesSeasonId",
                table: "SeriesWatchHistory",
                column: "SeriesSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesWatchHistory_ServerId",
                table: "SeriesWatchHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "idx_serverseries_serverid_seriesid",
                table: "ServerSeries",
                columns: new[] { "ServerId", "SeriesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSeries_SeriesId",
                table: "ServerSeries",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "idx_serverseriesseasons_serverid_seasonid",
                table: "ServerSeriesSeasons",
                columns: new[] { "ServerId", "SeriesSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSeriesSeasons_SeriesSeasonId",
                table: "ServerSeriesSeasons",
                column: "SeriesSeasonId");

            migrationBuilder.CreateIndex(
                name: "idx_userseriesprofiles_userid",
                table: "UserSeriesProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Swipes_Series_SeriesId",
                table: "Swipes",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Watchlists_Series_SeriesId",
                table: "Watchlists",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Swipes_Series_SeriesId",
                table: "Swipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Watchlists_Series_SeriesId",
                table: "Watchlists");

            migrationBuilder.DropTable(
                name: "SeriesWatchHistory");

            migrationBuilder.DropTable(
                name: "ServerSeries");

            migrationBuilder.DropTable(
                name: "ServerSeriesSeasons");

            migrationBuilder.DropTable(
                name: "UserSeriesProfiles");

            migrationBuilder.DropTable(
                name: "SeriesSeasons");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Watchlists_SeriesId",
                table: "Watchlists");

            migrationBuilder.DropCheckConstraint(
                name: "ck_watchlists_movie_xor_series",
                table: "Watchlists");

            migrationBuilder.DropIndex(
                name: "IX_Swipes_SeriesId",
                table: "Swipes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_swipes_movie_xor_series",
                table: "Swipes");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Watchlists");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Swipes");

            migrationBuilder.RenameIndex(
                name: "idx_watchlists_userid",
                table: "Watchlists",
                newName: "IX_Watchlists_UserId");

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "Watchlists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "Swipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
