using Microsoft.EntityFrameworkCore.Migrations;
using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackAlbum : EFMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Album",
                table: "Tracks",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Album",
                table: "Tracks",
                column: "Album");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tracks_Album",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Album",
                table: "Tracks");
        }
    }
}
