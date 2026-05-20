using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Repositories;
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

        // Фаза 2: пользовательские данные ещё до апгрейда. Используем репозиторий,
        // а не сырой EF — он ближе к реальному пути добавления треков в проде.
        var repo = new SqliteTrackRepository(factory.CreateContext);
        for (int i = 1; i <= 5; i++)
        {
            repo.Add(new Track
            {
                Title = $"Track {i}",
                Artist = $"artist{i}",
                Album = $"album{i}",
                FilePath = $"{i}.mp3"
            });
        }

        // Фаза 3: апгрейд до 1.0.2 — создаётся TracksFts и выполняется backfill
        // существующих строк через INSERT INTO TracksFts SELECT FROM Tracks.
        factory.MigrateUpTo(MigrationAddTracksFts);

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

        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Old Song", Artist = "Pre Upgrade", FilePath = "1.mp3" });

        factory.MigrateUpTo(MigrationAddTracksFts);

        // Старая запись попала в индекс через backfill.
        var service = new SqliteFtsSearchService(factory.CreateContext);
        Assert.Single(service.Search("pre"));

        // А новые вставки после апгрейда должны индексироваться триггером Tracks_ai —
        // backfill ничего не должен ломать в обычном run-time пути.
        repo.Add(new Track { Title = "Fresh Song", Artist = "Post Upgrade", FilePath = "2.mp3" });

        Assert.Single(service.Search("post"));
        Assert.Single(service.Search("fresh"));
    }
}
