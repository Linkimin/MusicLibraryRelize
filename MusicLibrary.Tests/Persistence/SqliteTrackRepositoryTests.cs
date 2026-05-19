using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqliteTrackRepositoryTests
{
    [Fact]
    public void Add_AssignsId_And_PersistsTrack()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        var track = new Track
        {
            Title = "Test",
            Artist = "Author",
            Genre = "Rock",
            Duration = TimeSpan.FromSeconds(180),
            FilePath = @"C:\Music\test.mp3",
            CoverPath = @"C:\Covers\test.png"
        };

        var saved = repository.Add(track);

        Assert.True(saved.Id > 0);

        var all = repository.GetAll();
        Assert.Single(all);
        Assert.Equal("Test", all[0].Title);
    }

    [Fact]
    public void GetAll_Returns_Empty_For_Fresh_Database()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void FindById_Returns_Null_For_Unknown_Id()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        Assert.Null(repository.FindById(999));
    }

    [Fact]
    public void Remove_Deletes_Track()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        var saved = repository.Add(new Track
        {
            Title = "X",
            Artist = "Y",
            Duration = TimeSpan.FromSeconds(60),
            FilePath = "x"
        });

        repository.Remove(saved.Id);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void GetAll_Orders_BuiltIn_Tracks_First()
    {
        using var factory = new InMemorySqliteDbContextFactory();

        // Записываем напрямую через EF, чтобы поставить IsBuiltIn=true.
        using (var ctx = factory.CreateContext())
        {
            ctx.Tracks.AddRange(
                new TrackEntity { Title = "User", Artist = "U", FilePath = "u", IsBuiltIn = false },
                new TrackEntity { Title = "Built", Artist = "B", FilePath = "b", IsBuiltIn = true });
            ctx.SaveChanges();
        }

        var repository = new SqliteTrackRepository(factory.CreateContext);
        var all = repository.GetAll();

        Assert.Equal("Built", all[0].Title);
        Assert.Equal("User", all[1].Title);
    }
}
