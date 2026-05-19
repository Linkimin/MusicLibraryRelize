using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicBakh.Infrastructure.Seeding;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Seeding;

public sealed class BuiltInTrackSeederTests
{
    [Fact]
    public void Seed_Adds_Tracks_To_Empty_Database()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);

        var tracks = new[]
        {
            new Track { Title = "Seed 1", Artist = "A", FilePath = "1.mp3", IsBuiltIn = true },
            new Track { Title = "Seed 2", Artist = "B", FilePath = "2.mp3", IsBuiltIn = true }
        };
        var seeder = new BuiltInTrackSeeder(repo, () => tracks);

        seeder.SeedBuiltIns();

        Assert.Equal(2, repo.GetAll().Count);
        Assert.All(repo.GetAll(), t => Assert.True(t.IsBuiltIn));
    }

    [Fact]
    public void Seed_Adds_Built_Ins_Even_When_User_Tracks_Exist()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);

        // Симулируем последствие JsonToSqliteMigrationService: пользовательские треки уже в БД,
        // но эталонных встроенных нет.
        repo.Add(new Track { Title = "User", Artist = "X", FilePath = "u.mp3", IsBuiltIn = false });

        var seeds = new[] { new Track { Title = "Seed", Artist = "S", FilePath = "s.mp3", IsBuiltIn = true } };
        var seeder = new BuiltInTrackSeeder(repo, () => seeds);

        seeder.SeedBuiltIns();

        var all = repo.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, t => t.IsBuiltIn && t.Title == "Seed");
        Assert.Contains(all, t => !t.IsBuiltIn && t.Title == "User");
    }

    [Fact]
    public void Seed_Is_Idempotent_For_Already_Present_Built_Ins()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);

        var seeds = new[]
        {
            new Track { Title = "Seed 1", Artist = "A", FilePath = "1.mp3", IsBuiltIn = true },
            new Track { Title = "Seed 2", Artist = "B", FilePath = "2.mp3", IsBuiltIn = true }
        };
        var seeder = new BuiltInTrackSeeder(repo, () => seeds);

        seeder.SeedBuiltIns();
        seeder.SeedBuiltIns();

        // Повторный запуск не дублирует записи.
        Assert.Equal(2, repo.GetAll().Count);
    }
}
