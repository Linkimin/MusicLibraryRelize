using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Models;
using MusicLibrary.Services.Storage;
using MusicLibrary.Services.Tracks;

namespace MusicLibrary.Tests;

public sealed class CompositeTrackRepositoryTests
{
    [Fact]
    public void GetAll_MergesBuiltInAndUserTracks()
    {
        var builtIn = new FakeRepo(new Track { Id = 1, Title = "A", Genre = "Rock" });
        var storage = new FakeStorage(new UserTrack { Id = 5, Title = "User", Genre = "Pop", DurationSeconds = 100, FilePath = "u.mp3", CoverPath = "u.png" });
        var composite = new CompositeTrackRepository(builtIn, storage);

        IReadOnlyList<Track> all = composite.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal("A", all[0].Title);
        Assert.Equal("User", all[1].Title);
        Assert.Equal(5, all[1].Id);
    }

    [Fact]
    public void GetAll_ReassignsConflictingIds()
    {
        var builtIn = new FakeRepo(
            new Track { Id = 1, Title = "Built1" },
            new Track { Id = 2, Title = "Built2" });
        var storage = new FakeStorage(
            new UserTrack { Id = 1, Title = "Conflict", DurationSeconds = 1, FilePath = "x", CoverPath = "y" });
        var composite = new CompositeTrackRepository(builtIn, storage);

        IReadOnlyList<Track> all = composite.GetAll();

        Assert.Equal(3, all.Count);
        Track conflict = all[2];
        Assert.Equal("Conflict", conflict.Title);
        Assert.Equal(3, conflict.Id);
    }

    [Fact]
    public void GetAll_DeduplicatesUserIdsAmongThemselves()
    {
        var builtIn = new FakeRepo(new Track { Id = 1 });
        var storage = new FakeStorage(
            new UserTrack { Id = 2, Title = "First", DurationSeconds = 1, FilePath = "f1", CoverPath = "c1" },
            new UserTrack { Id = 2, Title = "Second", DurationSeconds = 1, FilePath = "f2", CoverPath = "c2" });
        var composite = new CompositeTrackRepository(builtIn, storage);

        IReadOnlyList<Track> all = composite.GetAll();

        var ids = all.Select(t => t.Id).ToHashSet();
        Assert.Equal(3, ids.Count);
    }

    private sealed class FakeRepo : ITrackRepository
    {
        private readonly Track[] _tracks;
        public FakeRepo(params Track[] tracks) => _tracks = tracks;
        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) => throw new NotSupportedException();
        public void Remove(int id) => throw new NotSupportedException();
    }

    private sealed class FakeStorage : IUserTrackStorage
    {
        private readonly List<UserTrack> _tracks;
        public FakeStorage(params UserTrack[] tracks) => _tracks = tracks.ToList();

        public string MusicDirectory => string.Empty;
        public string CoversDirectory => string.Empty;
        public IReadOnlyList<UserTrack> Load() => _tracks;
        public void Save(IEnumerable<UserTrack> tracks) { _tracks.Clear(); _tracks.AddRange(tracks); }
        public void Append(UserTrack track) => _tracks.Add(track);
        public void Delete(int id) => _tracks.RemoveAll(t => t.Id == id);
    }
}
