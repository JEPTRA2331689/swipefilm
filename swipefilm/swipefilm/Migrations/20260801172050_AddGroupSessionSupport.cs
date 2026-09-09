using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace swipefilm.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupSessionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_AspNetUsers_CreatedById",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_CreatedById",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Sessions");

            migrationBuilder.RenameIndex(
                name: "IX_SessionMatches_SessionId",
                table: "SessionMatches",
                newName: "idx_sessionmatches_sessionid");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Swipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentTypeFilter",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string[]>(
                name: "GenreFilter",
                table: "Sessions",
                type: "text[]",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "SessionMatches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "SessionMatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Swipes_SessionId",
                table: "Swipes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_code",
                table: "Sessions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedByUserId",
                table: "Sessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionMatches_SeriesId",
                table: "SessionMatches",
                column: "SeriesId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sessionmatches_movie_xor_series",
                table: "SessionMatches",
                sql: "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionMatches_Series_SeriesId",
                table: "SessionMatches",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_AspNetUsers_CreatedByUserId",
                table: "Sessions",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Swipes_Sessions_SessionId",
                table: "Swipes",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionMatches_Series_SeriesId",
                table: "SessionMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_AspNetUsers_CreatedByUserId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Swipes_Sessions_SessionId",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "IX_Swipes_SessionId",
                table: "Swipes");

            migrationBuilder.DropIndex(
                name: "idx_sessions_code",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_CreatedByUserId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionMatches_SeriesId",
                table: "SessionMatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sessionmatches_movie_xor_series",
                table: "SessionMatches");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Swipes");

            migrationBuilder.DropColumn(
                name: "ContentTypeFilter",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "GenreFilter",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "SessionMatches");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "AspNetUsers");

            migrationBuilder.RenameIndex(
                name: "idx_sessionmatches_sessionid",
                table: "SessionMatches",
                newName: "IX_SessionMatches_SessionId");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "MovieId",
                table: "SessionMatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedById",
                table: "Sessions",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_AspNetUsers_CreatedById",
                table: "Sessions",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
