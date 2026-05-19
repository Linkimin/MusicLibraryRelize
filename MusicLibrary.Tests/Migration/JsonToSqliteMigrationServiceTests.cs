using System.IO;
using MusicBakh.Infrastructure.Migration;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Migration;

public sealed class JsonToSqliteMigrationServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public JsonToSqliteMigrationServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "musicbakh-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Файл может быть занят, не критично — это временный каталог тестов.
            }
        }
    }

    [Fact]
    public void Run_Does_Nothing_When_Legacy_File_Missing()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, repo);

        var result = migration.Run();

        Assert.False(result.PerformedMigration);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Run_Migrates_Tracks_And_Renames_Legacy_File()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, """
            [
              {
                "id": 1,
                "title": "Test Song",
                "artist": "Test Artist",
                "genre": "Rock",
                "durationSeconds": 180,
                "filePath": "C:/Music/test.mp3",
                "coverPath": "C:/Covers/test.png",
                "addedAt": "2026-01-01T12:00:00Z"
              }
            ]
            """);

        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, repo);

        var result = migration.Run();

        Assert.True(result.PerformedMigration);
        Assert.Equal(1, result.MigratedTracks);
        Assert.False(File.Exists(legacyPath), "userTracks.json должен быть переименован после миграции");
        Assert.True(File.Exists(result.BackupPath!), "должен существовать backup-файл");

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("Test Song", all[0].Title);
    }

    [Fact]
    public void Run_Is_NoOp_If_Already_Migrated()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, "[]");

        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, repo);

        migration.Run();
        var second = migration.Run();

        Assert.False(second.PerformedMigration);
    }

    [Fact]
    public void Run_Handles_Corrupted_Json_Gracefully()
    {
        var legacyPath = Path.Combine(_tempRoot, "userTracks.json");
        File.WriteAllText(legacyPath, "{ not valid json");

        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        var migration = new JsonToSqliteMigrationService(_tempRoot, repo);

        var result = migration.Run();

        // признаём, что файл существовал; но записей не получили
        Assert.True(result.PerformedMigration);
        Assert.Equal(0, result.MigratedTracks);
        Assert.True(File.Exists(result.BackupPath!));
    }
}
