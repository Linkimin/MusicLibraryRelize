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
