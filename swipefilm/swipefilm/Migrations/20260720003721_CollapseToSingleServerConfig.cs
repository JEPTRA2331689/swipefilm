using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class CollapseToSingleServerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ Créée avant de supprimer UserServers pour pouvoir y copier la
            // config déjà en place (URL/token chiffrés) — sinon l'admin
            // devrait tout reconfigurer depuis zéro après cette migration.
            migrationBuilder.CreateTable(
                name: "ServerConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FriendlyName = table.Column<string>(type: "text", nullable: false),
                    UrlEncrypted = table.Column<string>(type: "text", nullable: false),
                    TokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MachineIdentifier = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerConfig", x => x.Id);
                });

            // ✅ Toutes les lignes UserServers pointent vers le même serveur
            // physique (même Url/Token chiffrés, voir AuthController.Login —
            // "n'importe lequel, ils partagent le même") — on en copie une
            // seule dans le nouveau singleton avant de supprimer la table.
            migrationBuilder.Sql(@"
                INSERT INTO ""ServerConfig""
                    (""Id"", ""Type"", ""FriendlyName"", ""UrlEncrypted"", ""TokenEncrypted"",
                     ""LastSyncAt"", ""CreatedAt"", ""MachineIdentifier"")
                SELECT ""Id"", ""Type"", ""FriendlyName"", ""UrlEncrypted"", ""TokenEncrypted"",
                       ""LastSyncAt"", ""CreatedAt"", ""MachineIdentifier""
                FROM ""UserServers""
                WHERE ""IsActive"" = true
                ORDER BY ""CreatedAt""
                LIMIT 1;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_SeriesWatchHistory_UserServers_ServerId",
                table: "SeriesWatchHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerMovie_UserServers_ServerId",
                table: "ServerMovie");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerSeries_UserServers_ServerId",
                table: "ServerSeries");

            migrationBuilder.DropForeignKey(
                name: "FK_ServerSeriesSeasons_UserServers_ServerId",
                table: "ServerSeriesSeasons");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistory_UserServers_ServerId",
                table: "WatchHistory");

            migrationBuilder.DropTable(
                name: "UserServers");

            migrationBuilder.DropIndex(
                name: "idx_watchhistory_serverid",
                table: "WatchHistory");

            migrationBuilder.DropIndex(
                name: "idx_watchhistory_userid_serverid",
                table: "WatchHistory");

            migrationBuilder.DropIndex(
                name: "idx_serverseriesseasons_serverid_seasonid",
                table: "ServerSeriesSeasons");

            migrationBuilder.DropIndex(
                name: "IX_ServerSeriesSeasons_SeriesSeasonId",
                table: "ServerSeriesSeasons");

            migrationBuilder.DropIndex(
                name: "idx_serverseries_serverid_seriesid",
                table: "ServerSeries");

            migrationBuilder.DropIndex(
                name: "IX_ServerSeries_SeriesId",
                table: "ServerSeries");

            migrationBuilder.DropIndex(
                name: "idx_servermovie_server_movie",
                table: "ServerMovie");

            migrationBuilder.DropIndex(
                name: "IX_ServerMovie_MovieId",
                table: "ServerMovie");

            migrationBuilder.DropIndex(
                name: "idx_serieswatchhistory_userid_serverid",
                table: "SeriesWatchHistory");

            migrationBuilder.DropIndex(
                name: "IX_SeriesWatchHistory_ServerId",
                table: "SeriesWatchHistory");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "WatchHistory");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "ServerSeriesSeasons");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "ServerSeries");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "ServerMovie");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "SeriesWatchHistory");

            // ✅ L'ancien modèle avait une ligne ServerMovie/ServerSeries/
            // ServerSeriesSeasons par (serveur, item) — comme chaque utilisateur
            // importé avait son propre UserServer pointant vers le même serveur
            // physique, le même film/série/saison existe en plusieurs
            // exemplaires (même ServerItemId, juste dupliqué par ancien
            // ServerId). On garde une seule ligne par item avant de poser les
            // nouveaux index uniques sur MovieId/SeriesId/SeriesSeasonId seuls.
            migrationBuilder.Sql(@"
                DELETE FROM ""ServerMovie"" a
                USING ""ServerMovie"" b
                WHERE a.""Id"" > b.""Id"" AND a.""MovieId"" = b.""MovieId"";
            ");

            migrationBuilder.Sql(@"
                DELETE FROM ""ServerSeries"" a
                USING ""ServerSeries"" b
                WHERE a.""Id"" > b.""Id"" AND a.""SeriesId"" = b.""SeriesId"";
            ");

            migrationBuilder.Sql(@"
                DELETE FROM ""ServerSeriesSeasons"" a
                USING ""ServerSeriesSeasons"" b
                WHERE a.""Id"" > b.""Id"" AND a.""SeriesSeasonId"" = b.""SeriesSeasonId"";
            ");

            migrationBuilder.CreateIndex(
                name: "idx_serverseriesseasons_seasonid",
                table: "ServerSeriesSeasons",
                column: "SeriesSeasonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_serverseries_series",
                table: "ServerSeries",
                column: "SeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_servermovie_movie",
                table: "ServerMovie",
                column: "MovieId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerConfig");

            migrationBuilder.DropIndex(
                name: "idx_serverseriesseasons_seasonid",
                table: "ServerSeriesSeasons");

            migrationBuilder.DropIndex(
                name: "idx_serverseries_series",
                table: "ServerSeries");

            migrationBuilder.DropIndex(
                name: "idx_servermovie_movie",
                table: "ServerMovie");

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "WatchHistory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "ServerSeriesSeasons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "ServerSeries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "ServerMovie",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "SeriesWatchHistory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "UserServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FriendlyName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MachineIdentifier = table.Column<string>(type: "text", nullable: true),
                    TokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    UrlEncrypted = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserServers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_watchhistory_serverid",
                table: "WatchHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "idx_watchhistory_userid_serverid",
                table: "WatchHistory",
                columns: new[] { "UserId", "ServerId" });

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
                name: "idx_serverseries_serverid_seriesid",
                table: "ServerSeries",
                columns: new[] { "ServerId", "SeriesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSeries_SeriesId",
                table: "ServerSeries",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "idx_servermovie_server_movie",
                table: "ServerMovie",
                columns: new[] { "ServerId", "MovieId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerMovie_MovieId",
                table: "ServerMovie",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "idx_serieswatchhistory_userid_serverid",
                table: "SeriesWatchHistory",
                columns: new[] { "UserId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesWatchHistory_ServerId",
                table: "SeriesWatchHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "idx_userservers_userid_isactive",
                table: "UserServers",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_SeriesWatchHistory_UserServers_ServerId",
                table: "SeriesWatchHistory",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerMovie_UserServers_ServerId",
                table: "ServerMovie",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerSeries_UserServers_ServerId",
                table: "ServerSeries",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServerSeriesSeasons_UserServers_ServerId",
                table: "ServerSeriesSeasons",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistory_UserServers_ServerId",
                table: "WatchHistory",
                column: "ServerId",
                principalTable: "UserServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
