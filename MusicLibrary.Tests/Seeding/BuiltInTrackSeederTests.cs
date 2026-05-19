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

    [Fact]
    public void Seed_Refreshes_Diverged_Paths_Of_Existing_Built_Ins()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);

        // Старый путь — например, БД от предыдущей сборки в bin/Debug.
        var stale = new[]
        {
            new Track { Title = "Anthem", Artist = "Vendor", FilePath = @"C:\old\path\anthem.mp3", CoverPath = @"C:\old\path\anthem.jpg", IsBuiltIn = true }
        };
        new BuiltInTrackSeeder(repo, () => stale).SeedBuiltIns();
        Assert.Single(repo.GetAll());

        // Новый запуск из другого каталога: AppContext.BaseDirectory изменилась → новые пути.
        var fresh = new[]
        {
            new Track { Title = "Anthem", Artist = "Vendor", FilePath = @"C:\new\path\anthem.mp3", CoverPath = @"C:\new\path\anthem.jpg", IsBuiltIn = true }
        };
        new BuiltInTrackSeeder(repo, () => fresh).SeedBuiltIns();

        var actual = Assert.Single(repo.GetAll());
        Assert.Equal(@"C:\new\path\anthem.mp3", actual.FilePath);
        Assert.Equal(@"C:\new\path\anthem.jpg", actual.CoverPath);
        Assert.True(actual.IsBuiltIn);
    }

    [Fact]
    public void Seed_Does_Not_Touch_Matching_Built_Ins()
    {
        // Гарантия идемпотентности: если пути совпадают, Update не вызывается,
        // AddedAtUtc остаётся прежним (мы это явно не проверяем здесь, но Update
        // мог бы случайно сменить другие поля).
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);

        var seeds = new[]
        {
            new Track { Title = "X", Artist = "A", FilePath = "x.mp3", CoverPath = "x.jpg", IsBuiltIn = true }
        };
        new BuiltInTrackSeeder(repo, () => seeds).SeedBuiltIns();
        var idAfterFirst = repo.GetAll().Single().Id;

        new BuiltInTrackSeeder(repo, () => seeds).SeedBuiltIns();

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal(idAfterFirst, all[0].Id);
    }
}
