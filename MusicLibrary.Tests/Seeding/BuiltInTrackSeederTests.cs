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

        seeder.SeedIfEmpty();

        Assert.Equal(2, repo.GetAll().Count);
        Assert.All(repo.GetAll(), t => Assert.True(t.IsBuiltIn));
    }

    [Fact]
    public void Seed_Is_NoOp_When_Database_Has_Tracks()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteTrackRepository(factory.CreateContext);
        repo.Add(new Track { Title = "Existing", Artist = "X", FilePath = "x.mp3" });

        var tracks = new[] { new Track { Title = "Seed", Artist = "S", FilePath = "s.mp3", IsBuiltIn = true } };
        var seeder = new BuiltInTrackSeeder(repo, () => tracks);

        seeder.SeedIfEmpty();

        Assert.Single(repo.GetAll());
        Assert.Equal("Existing", repo.GetAll()[0].Title);
    }
}
