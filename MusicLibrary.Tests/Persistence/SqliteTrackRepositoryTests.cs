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
    public void Remove_Throws_WhenTrackIsBuiltIn()
    {
        using var factory = new InMemorySqliteDbContextFactory();

        int builtInId;
        using (var ctx = factory.CreateContext())
        {
            var entity = new TrackEntity
            {
                Title = "Anthem",
                Artist = "Vendor",
                FilePath = "vendor.mp3",
                IsBuiltIn = true
            };
            ctx.Tracks.Add(entity);
            ctx.SaveChanges();
            builtInId = entity.Id;
        }

        var repository = new SqliteTrackRepository(factory.CreateContext);

        Assert.Throws<NotSupportedException>(() => repository.Remove(builtInId));
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void Remove_Succeeds_ForUserTrack()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        var saved = repository.Add(new Track
        {
            Title = "User",
            Artist = "Self",
            Duration = TimeSpan.FromSeconds(120),
            FilePath = "self.mp3",
            IsBuiltIn = false
        });

        repository.Remove(saved.Id);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Remove_NoThrow_WhenTrackMissing()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        // Контракт ITrackRepository: «если трека с таким Id нет — тихо ничего не делает».
        repository.Remove(999);
    }

    [Fact]
    public void Add_Persists_Rating_And_Reaction()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        var saved = repository.Add(new Track
        {
            Title = "Hit",
            Artist = "Band",
            FilePath = "h.mp3",
            Rating = 4,
            Reaction = TrackReaction.Liked
        });

        var roundtrip = repository.FindById(saved.Id);
        Assert.NotNull(roundtrip);
        Assert.Equal(4, roundtrip!.Rating);
        Assert.Equal(TrackReaction.Liked, roundtrip.Reaction);
    }

    [Fact]
    public void Add_Defaults_Rating_To_Zero_And_Reaction_To_None()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);

        var saved = repository.Add(new Track { Title = "X", Artist = "Y", FilePath = "x.mp3" });

        Assert.Equal(0, saved.Rating);
        Assert.Equal(TrackReaction.None, saved.Reaction);
    }

    [Fact]
    public void Update_Changes_Rating_And_Reaction_Without_Resetting_Other_Fields()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repository = new SqliteTrackRepository(factory.CreateContext);
        var saved = repository.Add(new Track
        {
            Title = "Original",
            Artist = "Artist",
            Album = "Album",
            Genre = "Rock",
            FilePath = "f.mp3"
        });

        repository.Update(new Track
        {
            Id = saved.Id,
            Title = "Original",
            Artist = "Artist",
            Album = "Album",
            Genre = "Rock",
            FilePath = "f.mp3",
            Rating = 5,
            Reaction = TrackReaction.Disliked
        });

        var updated = repository.FindById(saved.Id);
        Assert.NotNull(updated);
        Assert.Equal(5, updated!.Rating);
        Assert.Equal(TrackReaction.Disliked, updated.Reaction);
        Assert.Equal("Original", updated.Title);
        Assert.Equal("Album", updated.Album);
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
