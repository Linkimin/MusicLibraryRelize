using System;
using Microsoft.EntityFrameworkCore.Migrations;
using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLibrarySchema : EFMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Genre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DurationTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CoverPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Artist",
                table: "Tracks",
                column: "Artist");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Genre",
                table: "Tracks",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_IsBuiltIn",
                table: "Tracks",
                column: "IsBuiltIn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tracks");
        }
    }
}
