using Microsoft.EntityFrameworkCore;
using MusicBakh.Infrastructure.Search;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Migrations;

/// <summary>
/// Регрессионные тесты на бэкфилл FTS-индекса при апгрейде 1.0.1 → 1.0.2. Закрывают
/// самый болезненный класс багов: «новые треки ищутся, а уже существовавшие до
/// апгрейда — нет». Сценарий буквально воспроизводит порядок шагов реального
/// апгрейда: схема 1.0.1 → данные → миграция AddTracksFts.
/// </summary>
public sealed class MigrationBackfillTests
{
    // Имена миграций — без расширений, в той форме, в которой EF Core хранит их
    // в __EFMigrationsHistory. См. файлы в MusicBakh.Infrastructure/Persistence/Migrations.
    private const string MigrationAddTrackAlbum = "20260519183130_AddTrackAlbum";
    private const string MigrationAddTracksFts = "20260519183826_AddTracksFts";

    [Fact]
    public void Migrate_From_1_0_1_State_Indexes_Existing_Tracks()
    {
        using var factory = new StagedMigrationsDbContextFactory();

        // Фаза 1: схема «как в 1.0.1» — последняя миграция перед FTS.
        factory.MigrateUpTo(MigrationAddTrackAlbum);

        // Фаза 2: пользовательские данные ещё до апгрейда. Вставляем через raw SQL,
        // а не через SqliteTrackRepository: репозиторий построен против ТЕКУЩЕЙ EF-модели
        // (с Rating/Reaction из 1.0.3), которые в схеме 1.0.1 ещё не существуют. Raw SQL
        // честнее имитирует данные, реально лежащие в БД пользователя на момент апгрейда.
        using (var ctx = factory.CreateContext())
        {
            for (int i = 1; i <= 5; i++)
            {
                ctx.Database.ExecuteSqlInterpolated($@"
                    INSERT INTO Tracks (Title, Artist, Album, Genre, DurationTicks, FilePath, CoverPath, AddedAtUtc, IsBuiltIn)
                    VALUES ({"Track " + i}, {"artist" + i}, {"album" + i}, '', 0, {i + ".mp3"}, '', '2026-01-01 00:00:00', 0)");
            }
        }

        // Фаза 3: апгрейд до 1.0.2 — создаётся TracksFts и выполняется backfill
        // существующих строк через INSERT INTO TracksFts SELECT FROM Tracks.
        factory.MigrateUpTo(MigrationAddTracksFts);

        // Фаза 4: достигаем head'а (применяем 1.0.3+ миграции, такие как
        // AddTrackRatingAndReaction). Эти миграции не трогают FTS-индекс, поэтому
        // backfill, проверяемый ниже, остаётся следствием именно AddTracksFts.
        // Без head'а EF-модель текущего кода рассинхронизирована со схемой.
        factory.MigrateUpToHead();

        var service = new SqliteFtsSearchService(factory.CreateContext);

        // Каждый из 5 заранее вставленных треков должен находиться через FTS — это
        // и есть проверка backfill'а: без INSERT'а в миграции FTS-таблица была бы
        // пустой и Search вернул бы 0 результатов.
        for (int i = 1; i <= 5; i++)
        {
            var hits = service.Search($"artist{i}");
            Assert.Single(hits);
            Assert.Equal($"Track {i}", hits[0].Title);
        }
    }

    [Fact]
    public void After_Backfill_New_Inserts_Are_Still_Indexed_By_Trigger()
    {
        using var factory = new StagedMigrationsDbContextFactory();

        factory.MigrateUpTo(MigrationAddTrackAlbum);

        // Pre-upgrade row через raw SQL — см. комментарий в Migrate_From_1_0_1_State_*.
        using (var ctx = factory.CreateContext())
        {
            ctx.Database.ExecuteSqlInterpolated($@"
                INSERT INTO Tracks (Title, Artist, Album, Genre, DurationTicks, FilePath, CoverPath, AddedAtUtc, IsBuiltIn)
                VALUES ('Old Song', 'Pre Upgrade', '', '', 0, '1.mp3', '', '2026-01-01 00:00:00', 0)");
        }

        factory.MigrateUpTo(MigrationAddTracksFts);

        // Достигаем head'а: дотягиваем 1.0.3+ миграции (AddTrackRatingAndReaction и т.д.).
        // FTS-индекс не трогается, backfill из AddTracksFts по-прежнему — единственный
        // путь попадания старых треков в TracksFts.
        factory.MigrateUpToHead();

        // Старая запись попала в индекс через backfill.
        var service = new SqliteFtsSearchService(factory.CreateContext);
        Assert.Single(service.Search("pre"));

        // А новые вставки через репозиторий должны попадать в TracksFts через триггер
        // Tracks_ai. Это в т.ч. проверка «backfill ничего не сломал в обычном пути».
        var repo = new MusicBakh.Infrastructure.Persistence.Repositories.SqliteTrackRepository(factory.CreateContext);
        repo.Add(new MusicBakh.Core.Domain.Track { Title = "Fresh Song", Artist = "Post Upgrade", FilePath = "2.mp3" });

        Assert.Single(service.Search("post"));
        Assert.Single(service.Search("fresh"));
    }
}
