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

    // === Task 6: PlayAlbum/PlayArtist/ShuffleAlbum/ShuffleArtist — сборка очереди воспроизведения ===

    [Fact]
    public void PlayAlbumCommand_Replaces_Queue_With_Album_Tracks_In_Order_And_Plays_First()
    {
        // Треки внутри альбома уже приходят от GroupByAlbum отсортированными
        // (TrackNumber ASC NULLS LAST, Title ASC) — здесь намеренно перемешан
        // порядок TrackNumber, чтобы убедиться, что VM не переупорядочивает очередь сама.
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", TrackNumber = 3, FilePath = "3.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.PlayAlbumCommand.Execute(album);

        Assert.Equal(album.Tracks.Select(t => t.Id), vm.DisplayedTracks.Select(t => t.Id));
        Assert.Equal(album.Tracks[0].Id, vm.SelectedTrack?.Id);
        Assert.Equal(album.Tracks[0].Id, vm.PlayingTrack?.Id);
    }

    [Fact]
    public void PlayArtistCommand_Flattens_Albums_Then_Loose_Tracks_In_Order()
    {
        // Два альбома (сортируются Year DESC NULLS LAST, Title ASC -> "Beta" (2021) раньше "Alpha" (2020))
        // и один loose-трек (Album пустой). Ожидаем: треки Beta, затем треки Alpha, затем loose.
        var vm = CreateVM(
            new Track { Id = 1, Title = "A1", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "A2", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "B1", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 1, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "B2", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 2, FilePath = "4.mp3" },
            new Track { Id = 5, Title = "Loose", Artist = "Art", Album = "", FilePath = "5.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];

        // Sanity: агрегат действительно содержит 2 альбома + 1 loose-трек, иначе тест ничего не проверяет.
        Assert.Equal(2, artist.Albums.Count);
        Assert.Single(artist.LooseTracks);

        vm.PlayArtistCommand.Execute(artist);

        var expectedOrder = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).Select(t => t.Id).ToList();
        Assert.Equal(new[] { 3, 4, 1, 2, 5 }, expectedOrder);
        Assert.Equal(expectedOrder, vm.DisplayedTracks.Select(t => t.Id));
        Assert.Equal(expectedOrder[0], vm.SelectedTrack?.Id);
        Assert.Equal(expectedOrder[0], vm.PlayingTrack?.Id);
    }

    [Fact]
    public void ShuffleAlbumCommand_Preserves_Track_Set_Without_Drop_Or_Duplicate()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "T1", Artist = "A", Album = "Alb", TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "T2", Artist = "A", Album = "Alb", TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "T3", Artist = "A", Album = "Alb", TrackNumber = 3, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "T4", Artist = "A", Album = "Alb", TrackNumber = 4, FilePath = "4.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        var album = vm.DisplayedAlbums[0];

        vm.ShuffleAlbumCommand.Execute(album);

        // Порядок случаен (Fisher-Yates) — проверяем только сохранность множества: без потерь и дублей.
        Assert.Equal(album.Tracks.Count, vm.DisplayedTracks.Count);
        Assert.Equal(
            album.Tracks.Select(t => t.Id).OrderBy(id => id),
            vm.DisplayedTracks.Select(t => t.Id).OrderBy(id => id));
        Assert.Contains(vm.DisplayedTracks, t => t.Id == vm.SelectedTrack!.Id);
    }

    [Fact]
    public void ShuffleArtistCommand_Preserves_Track_Set_Across_Albums_And_Loose_Tracks()
    {
        var vm = CreateVM(
            new Track { Id = 1, Title = "A1", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 1, FilePath = "1.mp3" },
            new Track { Id = 2, Title = "A2", Artist = "Art", Album = "Alpha", Year = 2020, TrackNumber = 2, FilePath = "2.mp3" },
            new Track { Id = 3, Title = "B1", Artist = "Art", Album = "Beta", Year = 2021, TrackNumber = 1, FilePath = "3.mp3" },
            new Track { Id = 4, Title = "Loose", Artist = "Art", Album = "", FilePath = "4.mp3" });
        vm.SwitchViewCommand.Execute(MainViewMode.Artists);
        var artist = vm.DisplayedArtists[0];
        var expectedIds = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).Select(t => t.Id).OrderBy(id => id).ToList();

        vm.ShuffleArtistCommand.Execute(artist);

        Assert.Equal(expectedIds.Count, vm.DisplayedTracks.Count);
        Assert.Equal(expectedIds, vm.DisplayedTracks.Select(t => t.Id).OrderBy(id => id));
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
