using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqliteListeningHistoryRepositoryTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Append_Saves_Entry_With_Clock_Timestamp()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "Song");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);

        repo.Append(new PlaybackEntry
        {
            Track = new Track { Id = 1, Title = "Song" },
            PlayedAt = DateTime.MinValue // должен быть проигнорирован, время берётся из IClock
        });

        var recent = repo.GetRecent();
        Assert.Single(recent);
        Assert.Equal(clock.UtcNow, recent[0].PlayedAt);
        Assert.Equal("Song", recent[0].Track.Title);
    }

    [Fact]
    public void GetRecent_Limits_Results_And_Orders_Desc()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "A");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);

        for (int i = 0; i < 60; i++)
        {
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            repo.Append(new PlaybackEntry { Track = new Track { Id = 1 }, PlayedAt = DateTime.MinValue });
        }

        var recent = repo.GetRecent(limit: 50);
        Assert.Equal(50, recent.Count);
        Assert.True(recent[0].PlayedAt > recent[^1].PlayedAt);
    }

    [Fact]
    public void GetRecent_Preserves_IsBuiltIn_Flag()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        // Эталонный seed-трек: IsBuiltIn=true.
        using (var ctx = factory.CreateContext())
        {
            ctx.Tracks.Add(new TrackEntity
            {
                Id = 42,
                Title = "Built-in",
                Artist = "Vendor",
                FilePath = "built.mp3",
                IsBuiltIn = true
            });
            ctx.SaveChanges();
        }

        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, new FixedClock());
        repo.Append(new PlaybackEntry { Track = new Track { Id = 42 }, PlayedAt = DateTime.MinValue });

        var recent = repo.GetRecent();
        // Если IsBuiltIn потерян — UI разрешит удалить shipped-файлы из install-папки.
        Assert.True(recent[0].Track.IsBuiltIn,
            "IsBuiltIn должен дотечь до UI через историю, иначе delete-кнопка активируется на встроенных треках.");
    }

    [Fact]
    public void GetAll_Returns_Everything_Without_Limit_Ordered_Desc()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "A");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);

        for (int i = 0; i < 120; i++)
        {
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            repo.Append(new PlaybackEntry { Track = new Track { Id = 1 }, PlayedAt = DateTime.MinValue });
        }

        var all = repo.GetAll();
        Assert.Equal(120, all.Count);
        Assert.True(all[0].PlayedAt > all[^1].PlayedAt);
    }

    [Fact]
    public void GetTop_Counts_Plays_And_Orders_Desc()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "Popular");
        SeedTrack(factory, id: 2, title: "Sometimes");
        SeedTrack(factory, id: 3, title: "Rare");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);

        for (int i = 0; i < 5; i++) repo.Append(Entry(1, clock));
        for (int i = 0; i < 3; i++) repo.Append(Entry(2, clock));
        repo.Append(Entry(3, clock));

        var top = repo.GetTop(limit: 10);

        Assert.Equal(3, top.Count);
        Assert.Equal("Popular",   top[0].Track.Title);
        Assert.Equal(5,           top[0].PlayCount);
        Assert.Equal("Sometimes", top[1].Track.Title);
        Assert.Equal(3,           top[1].PlayCount);
        Assert.Equal("Rare",      top[2].Track.Title);
        Assert.Equal(1,           top[2].PlayCount);
    }

    [Fact]
    public void GetTop_Respects_Limit()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        for (int id = 1; id <= 5; id++)
        {
            SeedTrack(factory, id, $"T{id}");
        }

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);
        for (int id = 1; id <= 5; id++)
        {
            repo.Append(Entry(id, clock));
        }

        Assert.Equal(3, repo.GetTop(limit: 3).Count);
    }

    [Fact]
    public void GetRecentUnique_Dedups_By_TrackId_And_Orders_By_Last_Play()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "First");
        SeedTrack(factory, id: 2, title: "Second");
        SeedTrack(factory, id: 3, title: "Third");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);

        repo.Append(Entry(1, clock)); // First в 12:01
        repo.Append(Entry(2, clock)); // Second в 12:02
        repo.Append(Entry(1, clock)); // First в 12:03 — обновляет позицию
        repo.Append(Entry(3, clock)); // Third в 12:04

        var recent = repo.GetRecentUnique(limit: 10);

        Assert.Equal(3, recent.Count);
        Assert.Equal("Third",  recent[0].Track.Title);
        Assert.Equal("First",  recent[1].Track.Title); // Не Second, потому что у First последнее воспроизведение позже.
        Assert.Equal("Second", recent[2].Track.Title);
    }

    [Fact]
    public void GetNeverPlayed_Returns_Tracks_Without_History_Entries()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        SeedTrack(factory, id: 1, title: "Played");
        SeedTrack(factory, id: 2, title: "Untouched");

        var clock = new FixedClock();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, clock);
        repo.Append(Entry(1, clock));

        var never = repo.GetNeverPlayed();

        Assert.Single(never);
        Assert.Equal("Untouched", never[0].Title);
    }

    [Fact]
    public void GetNeverPlayed_Returns_Empty_When_Library_Empty()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqliteListeningHistoryRepository(factory.CreateContext, new FixedClock());

        Assert.Empty(repo.GetNeverPlayed());
    }

    private static PlaybackEntry Entry(int trackId, FixedClock clock)
    {
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        return new PlaybackEntry
        {
            Track = new Track { Id = trackId },
            PlayedAt = DateTime.MinValue
        };
    }

    private static void SeedTrack(InMemorySqliteDbContextFactory factory, int id, string title)
    {
        using var ctx = factory.CreateContext();
        ctx.Tracks.Add(new TrackEntity
        {
            Id = id,
            Title = title,
            Artist = "X",
            FilePath = "x"
        });
        ctx.SaveChanges();
    }
}
