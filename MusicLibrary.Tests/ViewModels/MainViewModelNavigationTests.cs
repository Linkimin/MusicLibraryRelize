using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using MusicLibrary.ViewModels;
using Xunit;
using System.IO;

namespace MusicLibrary.Tests.ViewModels;

public sealed class MainViewModelNavigationTests
{
    private static MainViewModel CreateVM(params Track[] tracks)
    {
        return new MainViewModel(
            new FakeTrackRepo(tracks),
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            new FakeAudioPlayerService(),
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null,
            searchService: null,
            statsWindowService: null,
            tagRepository: null);
    }

    [Fact]
    public void Default_State_Is_TracksRoot()
    {
        var vm = CreateVM();
        Assert.IsType<LeftColumnState.TracksRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void SwitchView_Changes_Root_And_Clears_Back_Stack()
    {
        var vm = CreateVM();

        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        Assert.IsType<LeftColumnState.AlbumsRoot>(vm.CurrentLeftColumn);

        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        Assert.IsType<LeftColumnState.ArtistsRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void OpenAlbum_From_Albums_Root_Pushes_Detail_And_Sets_CanGoBack()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.OpenAlbumCommand.Execute(album);

        var detail = Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);
        Assert.Same(album, detail.Album);
        Assert.True(vm.CanGoBack);
    }

    [Fact]
    public void Back_From_Album_Detail_Returns_To_Albums_Root()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);

        vm.BackCommand.Execute(null);

        Assert.IsType<LeftColumnState.AlbumsRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void Artist_Then_Album_Then_Back_Returns_To_Artist_Detail()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);

        var artist = vm.DisplayedArtists[0];
        vm.OpenArtistCommand.Execute(artist);

        var album = artist.Albums[0];
        vm.OpenAlbumCommand.Execute(album);

        // Sanity: глубокий drill
        Assert.IsType<LeftColumnState.AlbumDetail>(vm.CurrentLeftColumn);

        vm.BackCommand.Execute(null);

        var detail = Assert.IsType<LeftColumnState.ArtistDetail>(vm.CurrentLeftColumn);
        Assert.Same(artist, detail.Artist);
        Assert.True(vm.CanGoBack); // ещё один back возвращает на ArtistsRoot
    }

    [Fact]
    public void SwitchView_While_Drilled_In_Resets_Back_Stack()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", FilePath = "2.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        Assert.True(vm.CanGoBack);

        vm.SwitchViewCommand.Execute(MainViewMode.Tracks);

        Assert.IsType<LeftColumnState.TracksRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    // === Фабрика VM и минимальные фейки (скопированы из MainViewModelTagAttachTests, т.к. там private) ===

    private sealed class FakeTrackRepo : ITrackRepository
    {
        private readonly List<Track> _tracks;
        public FakeTrackRepo(IEnumerable<Track> seed) => _tracks = seed.ToList();
        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) { _tracks.Add(track); return track; }
        public void Update(Track track) { int i = _tracks.FindIndex(t => t.Id == track.Id); if (i >= 0) _tracks[i] = track; }
        public void Remove(int id) => _tracks.RemoveAll(t => t.Id == id);
    }

    private sealed class FakeFileService : IFileService
    {
        public bool Exists(string path) => true;
        public OperationResult Copy(string sourcePath, string targetPath, bool overwrite) => OperationResult.Success("ok");
        public string GetFileName(string path) => Path.GetFileName(path) ?? "track.mp3";
        public OperationResult Delete(string path) => OperationResult.Success("deleted");
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => null;
    }

    private sealed class FakeAudioPlayerService : IAudioPlayerService
    {
        public event EventHandler<string>? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<string>? MediaFailed;

        public bool IsPlaying => false;
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(100);
        public double Volume { get; set; } = 1;
        public bool IsMuted { get; set; }

        public OperationResult Open(string filePath) => OperationResult.Success("opened");
        public OperationResult Play() => OperationResult.Success("playing");
        public void Pause() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeListeningHistoryRepository : IListeningHistoryRepository
    {
        private readonly List<PlaybackEntry> _entries = new();
        public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50) => _entries.OrderByDescending(e => e.PlayedAt).Take(limit).ToList();
        public IReadOnlyList<PlaybackEntry> GetAll() => _entries.OrderByDescending(e => e.PlayedAt).ToList();
        public IReadOnlyList<ListeningStats> GetTop(int limit = 50) => Array.Empty<ListeningStats>();
        public IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50) => Array.Empty<PlaybackEntry>();
        public IReadOnlyList<Track> GetNeverPlayed() => Array.Empty<Track>();
        public void Append(PlaybackEntry entry) => _entries.Add(entry);
    }

    private sealed class FakePlayerSettingsRepository : IPlayerSettingsRepository
    {
        public PlayerSettings Load() => PlayerSettings.Default;
        public void Save(PlayerSettings settings) { }
        public int? LoadActiveViewIndex() => null;
        public void SaveActiveView(MainViewMode view) { }
    }
}
