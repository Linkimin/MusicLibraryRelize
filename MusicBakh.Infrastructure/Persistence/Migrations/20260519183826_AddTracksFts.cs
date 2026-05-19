using Microsoft.EntityFrameworkCore.Migrations;
using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Заводит виртуальную FTS5-таблицу TracksFts (external content), триггеры
    /// синхронизации с Tracks и backfill уже существующих строк.
    /// Детали инвариантов и edge-cases — в плане итерации B
    /// (docs/superpowers/plans/2026-05-19-1.0.2-iteration-b-fts5-search-extended-history.md).
    /// </summary>
    public partial class AddTracksFts : EFMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // unicode61 + remove_diacritics 2 → «Café» находится по «cafe», «Цой» по «цои».
            migrationBuilder.Sql(@"
CREATE VIRTUAL TABLE IF NOT EXISTS TracksFts USING fts5(
    Title, Artist, Album, Genre,
    content='Tracks',
    content_rowid='Id',
    tokenize = ""unicode61 remove_diacritics 2""
);");

            migrationBuilder.Sql(@"
INSERT INTO TracksFts(rowid, Title, Artist, Album, Genre)
SELECT Id,
       COALESCE(Title,  ''),
       COALESCE(Artist, ''),
       COALESCE(Album,  ''),
       COALESCE(Genre,  '')
FROM Tracks;");

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Tracks_ai AFTER INSERT ON Tracks
BEGIN
    INSERT INTO TracksFts(rowid, Title, Artist, Album, Genre)
    VALUES (new.Id,
            COALESCE(new.Title,  ''),
            COALESCE(new.Artist, ''),
            COALESCE(new.Album,  ''),
            COALESCE(new.Genre,  ''));
END;");

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Tracks_ad AFTER DELETE ON Tracks
BEGIN
    INSERT INTO TracksFts(TracksFts, rowid, Title, Artist, Album, Genre)
    VALUES ('delete',
            old.Id,
            COALESCE(old.Title,  ''),
            COALESCE(old.Artist, ''),
            COALESCE(old.Album,  ''),
            COALESCE(old.Genre,  ''));
END;");

            // AFTER UPDATE OF <cols> — триггер срабатывает только когда меняются
            // индексируемые колонки. Это экономит работу при апдейтах CoverPath/IsBuiltIn
            // и будущих LastPlayedAt/Rating.
            // TODO: список колонок ниже должен совпадать с колонками TracksFts выше.
            // При добавлении новой индексируемой колонки — обновить и virtual table,
            // и оба INSERT'а внутри триггера, и список после AFTER UPDATE OF.
            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Tracks_au AFTER UPDATE OF Title, Artist, Album, Genre ON Tracks
BEGIN
    INSERT INTO TracksFts(TracksFts, rowid, Title, Artist, Album, Genre)
    VALUES ('delete',
            old.Id,
            COALESCE(old.Title,  ''),
            COALESCE(old.Artist, ''),
            COALESCE(old.Album,  ''),
            COALESCE(old.Genre,  ''));
    INSERT INTO TracksFts(rowid, Title, Artist, Album, Genre)
    VALUES (new.Id,
            COALESCE(new.Title,  ''),
            COALESCE(new.Artist, ''),
            COALESCE(new.Album,  ''),
            COALESCE(new.Genre,  ''));
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_ai;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TracksFts;");
        }
    }
}
