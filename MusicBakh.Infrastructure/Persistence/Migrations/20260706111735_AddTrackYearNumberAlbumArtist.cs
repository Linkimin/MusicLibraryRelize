using Microsoft.EntityFrameworkCore.Migrations;
using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackYearNumberAlbumArtist : EFMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlbumArtist",
                table: "Tracks",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackNumber",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Year",
                table: "Tracks",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tracks_Year",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "AlbumArtist",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "TrackNumber",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Tracks");
        }
    }
}
