using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Playback;
using MusicLibrary.Commands;
using MusicLibrary.Services.Library;
using MusicLibrary.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicLibrary.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private const string AllGenres = "Все жанры";

    // Лимит относится только к виджету «недавнее» в правой колонке MainWindow:
    // показываем последние 50 событий (с дублями), потому что виджет — это
    // «что играло прямо сейчас», а не журнал. Полная история без лимита доступна
    // через IListeningHistoryRepository.GetAll и используется в StatsWindow.
    private const int MaxHistoryItems = 50;

    private readonly List<Track> _allTracks;
    private readonly ITrackRepository _trackRepository;
    private readonly IFileService _fileService;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IAddTrackDialogService? _addTrackDialogService;
    private readonly IConfirmationService? _confirmationService;
    private readonly IListeningHistoryRepository _listeningHistoryRepository;
    private readonly IPlayerSettingsRepository _playerSettingsRepository;
    private readonly ISearchService? _searchService;
    private readonly IStatsWindowService? _statsWindowService;
    private readonly ITagsWindowService? _tagsWindowService;
    private readonly ITagRepository? _tagRepository;
    private readonly DispatcherTimer _progressTimer;

    private string _selectedGenre = AllGenres;
    private string _searchText = string.Empty;
    private int _minRating;
    private TrackReaction? _reactionFilter;
    private MainViewMode _activeView = MainViewMode.Tracks;
    private IReadOnlyList<AlbumAggregate> _displayedAlbums = Array.Empty<AlbumAggregate>();
    private IReadOnlyList<ArtistAggregate> _displayedArtists = Array.Empty<ArtistAggregate>();
    private readonly Dictionary<int, ObservableCollection<Tag>> _tagsByTrackId = new();
    private readonly ObservableCollection<int> _selectedTagIds = new();
    private readonly ObservableCollection<TagFilterItem> _tagFilters = new();
    private Track? _selectedTrack;
    private Track? _playingTrack;
    private string _statusMessage = "Выберите трек для воспроизведения.";
    private OperationMessageKind _statusKind = OperationMessageKind.Info;
    private bool _isPlaying;
    private bool _isPaused;
    private Track? _pendingHistoryTrack;
    private TimeSpan _currentPosition;
    private TimeSpan _currentDuration;
    private RepeatMode _repeatMode;
    private double _volume = 1.0;
    private bool _isMuted;
    private bool _isSeeking;
    private readonly Stack<LeftColumnState> _navStack = new();
    private LeftColumnState _currentLeftColumn = new LeftColumnState.TracksRoot();
    private static readonly System.Random _shuffleRng = new();

    public MainViewModel(
        ITrackRepository trackRepository,
        IFileService fileService,
        ISaveFileDialogService saveFileDialogService,
        IAudioPlayerService audioPlayerService,
        IListeningHistoryRepository listeningHistoryRepository,
        IPlayerSettingsRepository playerSettingsRepository,
        IAddTrackDialogService? addTrackDialogService = null,
        IConfirmationService? confirmationService = null,
        ISearchService? searchService = null,
        IStatsWindowService? statsWindowService = null,
        ITagRepository? tagRepository = null,
        ITagsWindowService? tagsWindowService = null)
    {
        _trackRepository = trackRepository;
        _fileService = fileService;
        _saveFileDialogService = saveFileDialogService;
        _audioPlayerService = audioPlayerService;
        _listeningHistoryRepository = listeningHistoryRepository;
        _playerSettingsRepository = playerSettingsRepository;
        _addTrackDialogService = addTrackDialogService;
        _confirmationService = confirmationService;
        _searchService = searchService;
        _statsWindowService = statsWindowService;
        _tagsWindowService = tagsWindowService;
        _tagRepository = tagRepository;
        _selectedTagIds.CollectionChanged += (_, _) => ApplyFilters();
        LoadTagFilters();

        _allTracks = new List<Track>(trackRepository.GetAll());

        // Заполняем кэш тегов при старте: один запрос на трек.
        if (_tagRepository is not null)
        {
            foreach (var t in _allTracks)
            {
                _tagsByTrackId[t.Id] = new ObservableCollection<Tag>(_tagRepository.GetTagsForTrack(t.Id));
            }
        }

        DisplayedTracks = new ObservableCollection<Track>(_allTracks);
        // Начальные агрегаты считаем от полного списка треков (фильтров ещё нет).
        DisplayedAlbums = LibraryGroupingService.GroupByAlbum(_allTracks);
        DisplayedArtists = LibraryGroupingService.GroupByArtist(_allTracks);
        PlaybackHistory = new ObservableCollection<PlaybackEntry>();
        Genres = new ObservableCollection<string>(
            new[] { AllGenres }
                .Concat(_allTracks.Select(track => track.Genre).Distinct().OrderBy(genre => genre)));

        foreach (var entry in _listeningHistoryRepository.GetRecent(MaxHistoryItems))
        {
            PlaybackHistory.Add(entry);
        }

        PlayPauseCommand = new RelayCommand(_ => PlayOrPause(), _ => SelectedTrack is not null);
        StopCommand = new RelayCommand(_ => Stop(), _ => PlayingTrack is not null);
        SaveTrackCommand = new RelayCommand(_ => SaveSelectedTrack(), _ => SelectedTrack is not null);
        AddTrackCommand = new RelayCommand(_ => OpenAddTrackDialog(), _ => _addTrackDialogService is not null);
        DeleteTrackCommand = new RelayCommand(_ => DeleteSelectedTrack(), _ => CanDeleteSelected);
        PlayTrackCommand = new RelayCommand(
            parameter => PlaySpecificTrack(parameter as Track),
            parameter => parameter is Track);
        ReplayHistoryEntryCommand = new RelayCommand(
            parameter => ReplayHistoryEntry(parameter as PlaybackEntry),
            parameter => parameter is PlaybackEntry);
        OpenStatsCommand = new RelayCommand(
            _ => _statsWindowService?.Show(),
            _ => _statsWindowService is not null);
        OpenTagsCommand = new RelayCommand(
            _ => _tagsWindowService?.Show(),
            _ => _tagsWindowService is not null);
        // Рейтинг и реакция — пользовательская оценка, разрешена и для встроенных
        // треков (UI ограничивает только удаление seed-треков, не их оценку).
        SetRatingCommand = new RelayCommand(
            parameter => SetRatingOnSelected(parameter),
            _ => SelectedTrack is not null);
        SetReactionCommand = new RelayCommand(
            parameter => SetReactionOnSelected(parameter),
            _ => SelectedTrack is not null);
        ToggleTagFilterCommand = new RelayCommand(parameter => ToggleTagFilter(parameter as TagFilterItem));
        RefreshTagFiltersCommand = new RelayCommand(_ => LoadTagFilters());
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

        // Прикрепляем тег к выбранному треку и обновляем кэш.
        AttachTagToSelectedCommand = new RelayCommand(parameter =>
        {
            if (SelectedTrack is null || _tagRepository is null) return;
            if (parameter is not Tag tag) return;
            if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var existing) &&
                existing.Any(t => t.Id == tag.Id)) return;
            _tagRepository.AttachTag(SelectedTrack.Id, tag.Id);
            if (!_tagsByTrackId.ContainsKey(SelectedTrack.Id))
                _tagsByTrackId[SelectedTrack.Id] = new ObservableCollection<Tag>();
            _tagsByTrackId[SelectedTrack.Id].Add(tag);
            OnPropertyChanged(nameof(AvailableTagsForAttach));
        });

        // Открепляем тег от выбранного трека и обновляем кэш.
        DetachTagFromSelectedCommand = new RelayCommand(parameter =>
        {
            if (SelectedTrack is null || _tagRepository is null) return;
            if (parameter is not Tag tag) return;
            _tagRepository.DetachTag(SelectedTrack.Id, tag.Id);
            if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var collection))
            {
                var existingTag = collection.FirstOrDefault(t => t.Id == tag.Id);
                if (existingTag is not null) collection.Remove(existingTag);
            }
            OnPropertyChanged(nameof(AvailableTagsForAttach));
        });
        SetReactionFilterCommand = new RelayCommand(parameter => SetReactionFilter(parameter));
        SetMinRatingCommand = new RelayCommand(parameter =>
        {
            if (!TryParseInt(parameter, out int requested)) return;
            // Toggle: клик по уже активной звезде → 0 («Все»).
            // Сравниваем по зажатому значению: Execute("9") дважды = 5 → 0.
            int clamped = Math.Clamp(requested, 0, 5);
            MinRating = MinRating == clamped ? 0 : clamped;
        });

        SkipForwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(10)), _ => PlayingTrack is not null);
        SkipBackwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(-10)), _ => PlayingTrack is not null);
        PreviousTrackCommand = new RelayCommand(_ => GoToTrackByOffset(-1), _ => CanGoToOffset(-1));
        NextTrackCommand = new RelayCommand(_ => GoToTrackByOffset(+1), _ => CanGoToOffset(+1));
        ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
        CycleRepeatModeCommand = new RelayCommand(_ => CycleRepeatMode());
        SeekToCommand = new RelayCommand(parameter => SeekTo(parameter as TimeSpan?), _ => PlayingTrack is not null);

        // Навигация левой колонки: SwitchView сбрасывает back-стек и переустанавливает корень,
        // OpenAlbum/OpenArtist пушат текущее состояние перед drill-down, Back — снимает верхнее.
        SwitchViewCommand = new RelayCommand(p =>
        {
            if (p is not MainViewMode mode) return;
            ActiveView = mode;
            _navStack.Clear();
            CurrentLeftColumn = mode switch
            {
                MainViewMode.Tracks => new LeftColumnState.TracksRoot(),
                MainViewMode.Albums => new LeftColumnState.AlbumsRoot(),
                MainViewMode.Artists => new LeftColumnState.ArtistsRoot(),
                _ => new LeftColumnState.TracksRoot()
            };
            OnPropertyChanged(nameof(CanGoBack));
        });

        OpenAlbumCommand = new RelayCommand(p =>
        {
            if (p is not AlbumAggregate album) return;
            _navStack.Push(_currentLeftColumn);
            CurrentLeftColumn = new LeftColumnState.AlbumDetail(album);
            OnPropertyChanged(nameof(CanGoBack));
        });

        OpenArtistCommand = new RelayCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            _navStack.Push(_currentLeftColumn);
            CurrentLeftColumn = new LeftColumnState.ArtistDetail(artist);
            OnPropertyChanged(nameof(CanGoBack));
        });

        BackCommand = new RelayCommand(_ =>
        {
            if (_navStack.Count == 0) return;
            CurrentLeftColumn = _navStack.Pop();
            OnPropertyChanged(nameof(CanGoBack));
        }, _ => _navStack.Count > 0);

        PlayAlbumCommand = new RelayCommand(p =>
        {
            if (p is not AlbumAggregate album || album.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(album.Tracks, shuffle: false);
        });

        ShuffleAlbumCommand = new RelayCommand(p =>
        {
            if (p is not AlbumAggregate album || album.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(album.Tracks, shuffle: true);
        });

        PlayArtistCommand = new RelayCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            var all = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).ToList();
            if (all.Count == 0) return;
            ReplaceQueueAndPlay(all, shuffle: false);
        });

        ShuffleArtistCommand = new RelayCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            var all = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).ToList();
            if (all.Count == 0) return;
            ReplaceQueueAndPlay(all, shuffle: true);
        });

        _audioPlayerService.MediaOpened += (_, filePath) => HandleMediaOpened(filePath);
        _audioPlayerService.MediaEnded += (_, _) => HandleMediaEnded();
        _audioPlayerService.MediaFailed += (_, message) => HandleMediaFailed(message);

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _progressTimer.Tick += (_, _) => RefreshProgress();
    }

    public ObservableCollection<Track> DisplayedTracks { get; }
    public ObservableCollection<string> Genres { get; }
    public ObservableCollection<PlaybackEntry> PlaybackHistory { get; }

    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveTrackCommand { get; }
    public ICommand AddTrackCommand { get; }
    public ICommand DeleteTrackCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand ReplayHistoryEntryCommand { get; }
    public ICommand OpenStatsCommand { get; }
    public ICommand OpenTagsCommand { get; }
    public ICommand SetRatingCommand { get; }
    public ICommand SetReactionCommand { get; }
    public ICommand ToggleTagFilterCommand { get; }
    public ICommand RefreshTagFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand SetReactionFilterCommand { get; }
    public ICommand SetMinRatingCommand { get; }
    public ICommand AttachTagToSelectedCommand { get; }
    public ICommand DetachTagFromSelectedCommand { get; }
    public ICommand SwitchViewCommand { get; }
    public ICommand OpenAlbumCommand { get; }
    public ICommand OpenArtistCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand PlayAlbumCommand { get; }
    public ICommand ShuffleAlbumCommand { get; }
    public ICommand PlayArtistCommand { get; }
    public ICommand ShuffleArtistCommand { get; }

    /// <summary>Кэш тегов по треку. XAML-конвертер использует его для отображения чипов на карточке трека.</summary>
    public IReadOnlyDictionary<int, ObservableCollection<Tag>> TagsByTrackId => _tagsByTrackId;

    /// <summary>
    /// Теги, доступные для прикрепления к выбранному треку (глобальный список минус уже прикреплённые).
    /// Пересчитывается при смене SelectedTrack и при каждой операции attach/detach.
    /// </summary>
    public IEnumerable<Tag> AvailableTagsForAttach
    {
        get
        {
            if (SelectedTrack is null) return Array.Empty<Tag>();
            var attached = _tagsByTrackId.TryGetValue(SelectedTrack.Id, out var c)
                ? new HashSet<int>(c.Select(t => t.Id))
                : new HashSet<int>();
            return _tagFilters.Where(item => !attached.Contains(item.Tag.Id)).Select(item => item.Tag).ToList();
        }
    }

    /// <summary>Чипы тегов для левой колонки. Обновляются вручную через RefreshTagFiltersCommand.</summary>
    public ObservableCollection<TagFilterItem> TagFilters => _tagFilters;
    public ICommand SkipForwardCommand { get; }
    public ICommand SkipBackwardCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand CycleRepeatModeCommand { get; }
    public ICommand SeekToCommand { get; }

    public string SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (SetProperty(ref _selectedGenre, value))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Свободная строка поиска. Дебаунс делается на стороне XAML через
    /// Binding Delay=250, поэтому setter ViewModel-а просто применяет фильтр
    /// синхронно — это упрощает unit-тесты (без таймера).
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>Минимальный рейтинг трека для фильтра (0..5). 0 = фильтр не активен.</summary>
    public int MinRating
    {
        get => _minRating;
        set
        {
            int clamped = Math.Clamp(value, 0, 5);
            if (SetProperty(ref _minRating, clamped))
            {
                OnPropertyChanged(nameof(MinRatingDisplayText));
                ApplyFilters();
            }
        }
    }

    /// <summary>Подпись слайдера рейтинга: «Без фильтра» или «≥ N★».</summary>
    public string MinRatingDisplayText =>
        _minRating <= 0 ? "Без фильтра" : $"≥ {_minRating}★";

    /// <summary>Фильтр по реакции. null = «любая реакция».</summary>
    public TrackReaction? ReactionFilter
    {
        get => _reactionFilter;
        set
        {
            if (SetProperty(ref _reactionFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>Идентификаторы тегов, активных в фильтре (OR-семантика). UI добавляет/удаляет через эту коллекцию.</summary>
    public ObservableCollection<int> SelectedTagIds => _selectedTagIds;

    /// <summary>Активный режим левой колонки (треки/альбомы/исполнители). Persisted в KeyValueStore.</summary>
    public MainViewMode ActiveView
    {
        get => _activeView;
        set
        {
            if (SetProperty(ref _activeView, value))
            {
                _playerSettingsRepository?.SaveActiveView(value);
            }
        }
    }

    /// <summary>Computed-проекция отфильтрованных треков по альбомам (см. LibraryGroupingService). Пересчитывается в ApplyFilters.</summary>
    public IReadOnlyList<AlbumAggregate> DisplayedAlbums
    {
        get => _displayedAlbums;
        private set
        {
            if (!ReferenceEquals(_displayedAlbums, value))
            {
                _displayedAlbums = value;
                OnPropertyChanged(nameof(DisplayedAlbums));
            }
        }
    }

    /// <summary>Computed-проекция отфильтрованных треков по исполнителям (см. LibraryGroupingService). Пересчитывается в ApplyFilters.</summary>
    public IReadOnlyList<ArtistAggregate> DisplayedArtists
    {
        get => _displayedArtists;
        private set
        {
            if (!ReferenceEquals(_displayedArtists, value))
            {
                _displayedArtists = value;
                OnPropertyChanged(nameof(DisplayedArtists));
            }
        }
    }

    /// <summary>
    /// Текущее состояние левой колонки (drill-down: root/album-detail/artist-detail).
    /// Меняется командами SwitchView/OpenAlbum/OpenArtist/Back.
    /// </summary>
    public LeftColumnState CurrentLeftColumn
    {
        get => _currentLeftColumn;
        private set
        {
            if (!Equals(_currentLeftColumn, value))
            {
                _currentLeftColumn = value;
                OnPropertyChanged(nameof(CurrentLeftColumn));
            }
        }
    }

    /// <summary>Есть ли куда возвращаться по Back (back-стек непуст).</summary>
    public bool CanGoBack => _navStack.Count > 0;

    public Track? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (SetProperty(ref _selectedTrack, value))
            {
                OnPropertyChanged(nameof(HasSelectedTrack));
                OnPropertyChanged(nameof(IsSelectedPlaying));
                OnPropertyChanged(nameof(PlayPauseText));
                OnPropertyChanged(nameof(ShowOtherPlayingBadge));
                OnPropertyChanged(nameof(OtherPlayingText));
                OnPropertyChanged(nameof(CanDeleteSelected));
                OnPropertyChanged(nameof(AvailableTagsForAttach));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public Track? PlayingTrack
    {
        get => _playingTrack;
        private set
        {
            if (SetProperty(ref _playingTrack, value))
            {
                OnPropertyChanged(nameof(IsSelectedPlaying));
                OnPropertyChanged(nameof(PlayPauseText));
                OnPropertyChanged(nameof(ShowOtherPlayingBadge));
                OnPropertyChanged(nameof(OtherPlayingText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelectedTrack => SelectedTrack is not null;

    public bool CanDeleteSelected =>
        SelectedTrack is not null && IsUserTrack(SelectedTrack);

    private static bool IsUserTrack(Track track) => !track.IsBuiltIn;

    public bool IsSelectedPlaying =>
        SelectedTrack is not null
        && PlayingTrack is not null
        && SelectedTrack.Id == PlayingTrack.Id;

    public bool ShowOtherPlayingBadge =>
        PlayingTrack is not null
        && (SelectedTrack is null || SelectedTrack.Id != PlayingTrack.Id);

    public string OtherPlayingText =>
        PlayingTrack is null ? string.Empty : $"▶ играет: {PlayingTrack.Artist} — {PlayingTrack.Title}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public OperationMessageKind StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseText));
            }
        }
    }

    public string PlayPauseText => IsSelectedPlaying && _isPlaying ? "Пауза" : "Воспроизвести";

    public RepeatMode RepeatMode
    {
        get => _repeatMode;
        set
        {
            if (SetProperty(ref _repeatMode, value))
            {
                PersistSettings();
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            // Жёсткий clamp перед записью в плеер — UI слайдеру удобнее не знать про границы.
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (SetProperty(ref _volume, clamped))
            {
                _audioPlayerService.Volume = clamped;
                PersistSettings();
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _audioPlayerService.IsMuted = value;
                PersistSettings();
            }
        }
    }

    public bool IsSeeking
    {
        get => _isSeeking;
        set => SetProperty(ref _isSeeking, value);
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        private set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionText));
                OnPropertyChanged(nameof(ProgressValue));
            }
        }
    }

    public TimeSpan CurrentDuration
    {
        get => _currentDuration;
        private set
        {
            if (SetProperty(ref _currentDuration, value))
            {
                OnPropertyChanged(nameof(CurrentDurationText));
                OnPropertyChanged(nameof(ProgressMaximum));
            }
        }
    }

    public string CurrentPositionText => CurrentPosition.ToString(@"m\:ss");
    public string CurrentDurationText => CurrentDuration == TimeSpan.Zero ? "0:00" : CurrentDuration.ToString(@"m\:ss");
    public double ProgressValue => CurrentPosition.TotalSeconds;
    public double ProgressMaximum => Math.Max(CurrentDuration.TotalSeconds, 1);

    public int GetNextTrackId()
    {
        return _allTracks.Count == 0 ? 1 : _allTracks.Max(track => track.Id) + 1;
    }

    private void OpenAddTrackDialog()
    {
        if (_addTrackDialogService is null)
        {
            return;
        }

        TrackImportCandidate? candidate = _addTrackDialogService.Show();
        if (candidate is null)
        {
            return;
        }

        Track saved = _trackRepository.Add(new Track
        {
            Title = candidate.Title,
            Artist = candidate.Artist,
            Album = candidate.Album,
            Genre = candidate.Genre,
            Duration = candidate.Duration,
            FilePath = candidate.AudioFilePath,
            CoverPath = candidate.CoverFilePath,
            IsBuiltIn = false,
            Year = candidate.Year,
            TrackNumber = candidate.TrackNumber,
            AlbumArtist = candidate.AlbumArtist
        });

        AddTrack(saved);
        SetStatus(OperationResult.Success($"Трек «{saved.Title}» добавлен в библиотеку."));
    }

    private void DeleteSelectedTrack()
    {
        Track? track = SelectedTrack;
        if (track is null || !IsUserTrack(track))
        {
            return;
        }

        if (_confirmationService is not null)
        {
            bool confirmed = _confirmationService.Confirm(
                "Удалить трек",
                $"Удалить «{track.Artist} — {track.Title}» из библиотеки? Файл и обложка будут удалены безвозвратно.");
            if (!confirmed)
            {
                return;
            }
        }

        if (PlayingTrack is not null && PlayingTrack.Id == track.Id)
        {
            ResetPlaybackState();
        }

        _trackRepository.Remove(track.Id);
        _fileService.Delete(track.FilePath);
        _fileService.Delete(track.CoverPath);
        RemoveTrack(track);
        SetStatus(OperationResult.Success($"Трек «{track.Title}» удалён."));
    }

    private void RemoveTrack(Track track)
    {
        _allTracks.Remove(track);
        DisplayedTracks.Remove(track);

        // Если последний трек этого жанра ушёл — убираем жанр из фильтра.
        if (!_allTracks.Any(t => t.Genre == track.Genre) && Genres.Contains(track.Genre))
        {
            Genres.Remove(track.Genre);
            if (SelectedGenre == track.Genre)
            {
                SelectedGenre = AllGenres;
            }
        }

        if (SelectedTrack == track)
        {
            SelectedTrack = null;
        }

        // Удаляем запись кэша тегов вместе с треком.
        _tagsByTrackId.Remove(track.Id);
    }

    private void SetRatingOnSelected(object? parameter)
    {
        if (SelectedTrack is null) return;
        if (!TryParseInt(parameter, out int requested)) return;
        // Toggle: повторный клик по той же звезде сбрасывает рейтинг в 0.
        int next = SelectedTrack.Rating == requested ? 0 : Math.Clamp(requested, 0, 5);
        UpdateSelectedTrack(CloneTrack(SelectedTrack, rating: next));
    }

    private void SetReactionOnSelected(object? parameter)
    {
        if (SelectedTrack is null) return;
        TrackReaction requested = parameter switch
        {
            TrackReaction r => r,
            string s when Enum.TryParse<TrackReaction>(s, ignoreCase: true, out var parsed) => parsed,
            _ => TrackReaction.None
        };
        // Toggle: повторный клик по уже активной реакции сбрасывает в None.
        TrackReaction next = SelectedTrack.Reaction == requested ? TrackReaction.None : requested;
        UpdateSelectedTrack(CloneTrack(SelectedTrack, reaction: next));
    }

    // Track — sealed class с init-only свойствами, `with` для него не работает,
    // поэтому копируем вручную. Если в будущем Track станет record — можно убрать.
    private static Track CloneTrack(Track source, int? rating = null, TrackReaction? reaction = null) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Artist = source.Artist,
        Album = source.Album,
        Genre = source.Genre,
        Duration = source.Duration,
        FilePath = source.FilePath,
        CoverPath = source.CoverPath,
        Rating = rating ?? source.Rating,
        Reaction = reaction ?? source.Reaction,
        IsBuiltIn = source.IsBuiltIn,
        Year = source.Year,
        TrackNumber = source.TrackNumber,
        AlbumArtist = source.AlbumArtist
    };

    private void UpdateSelectedTrack(Track updated)
    {
        // Капчуем намерение ДО мутации коллекций. Иначе после
        // DisplayedTracks[i] = updated ListBox увидит Remove+Add, не найдёт
        // старый объект SelectedItem в коллекции и сбросит SelectedTrack в null
        // через TwoWay-биндинг — после этого мы уже не сможем понять, нужно ли
        // восстанавливать выделение.
        bool wasSelected = SelectedTrack?.Id == updated.Id;
        bool wasPlaying = PlayingTrack?.Id == updated.Id;

        _trackRepository.Update(updated);

        int allIndex = _allTracks.FindIndex(t => t.Id == updated.Id);
        if (allIndex >= 0) _allTracks[allIndex] = updated;

        int displayedIndex = -1;
        for (int i = 0; i < DisplayedTracks.Count; i++)
        {
            if (DisplayedTracks[i].Id == updated.Id) { displayedIndex = i; break; }
        }
        if (displayedIndex >= 0) DisplayedTracks[displayedIndex] = updated;

        if (wasSelected) SelectedTrack = updated;
        if (wasPlaying) PlayingTrack = updated;
    }

    private void LoadTagFilters()
    {
        if (_tagRepository is null) return;
        var fresh = _tagRepository.GetAll();
        var stillSelected = new HashSet<int>(_tagFilters.Where(f => f.IsSelected).Select(f => f.Tag.Id));
        _tagFilters.Clear();
        foreach (var tag in fresh)
        {
            _tagFilters.Add(new TagFilterItem(tag, stillSelected.Contains(tag.Id)));
        }
        // Если какой-то тег был удалён, убираем его id из активного фильтра.
        var freshIds = new HashSet<int>(fresh.Select(t => t.Id));
        for (int i = _selectedTagIds.Count - 1; i >= 0; i--)
        {
            if (!freshIds.Contains(_selectedTagIds[i]))
            {
                _selectedTagIds.RemoveAt(i);
            }
        }
    }

    private void ToggleTagFilter(TagFilterItem? item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected)
        {
            if (!_selectedTagIds.Contains(item.Tag.Id))
            {
                _selectedTagIds.Add(item.Tag.Id);
            }
        }
        else
        {
            _selectedTagIds.Remove(item.Tag.Id);
        }
    }

    private void SetReactionFilter(object? parameter)
    {
        // null/«Any» → выключить фильтр; «Liked»/«Disliked» → включить.
        // Повторный клик по активной кнопке сбрасывает в null (как и SetReactionCommand).
        if (parameter is null || parameter is string s1 && string.Equals(s1, "Any", StringComparison.OrdinalIgnoreCase))
        {
            ReactionFilter = null;
            return;
        }
        TrackReaction requested = parameter switch
        {
            TrackReaction r => r,
            string s when Enum.TryParse<TrackReaction>(s, ignoreCase: true, out var parsed) => parsed,
            _ => TrackReaction.None
        };
        ReactionFilter = ReactionFilter == requested ? null : requested;
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedGenre = AllGenres;
        MinRating = 0;
        ReactionFilter = null;
        foreach (var item in _tagFilters) item.IsSelected = false;
        _selectedTagIds.Clear();
    }

    private static bool TryParseInt(object? parameter, out int value)
    {
        if (parameter is int i) { value = i; return true; }
        if (parameter is string s &&
            int.TryParse(s, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    public void AddTrack(Track track)
    {
        _allTracks.Add(track);

        if (!Genres.Contains(track.Genre))
        {
            // Жанры отсортированы по алфавиту, «Все жанры» всегда первый.
            int insertIndex = 1;
            while (insertIndex < Genres.Count && string.CompareOrdinal(Genres[insertIndex], track.Genre) < 0)
            {
                insertIndex++;
            }
            Genres.Insert(insertIndex, track.Genre);
        }

        if (SelectedGenre == AllGenres || SelectedGenre == track.Genre)
        {
            DisplayedTracks.Add(track);
        }

        // Новый трек ещё не имеет тегов — инициализируем пустую коллекцию в кэше.
        _tagsByTrackId[track.Id] = new ObservableCollection<Tag>();
    }

    private void ApplyFilters()
    {
        // Собираем снимок активных критериев и отдаём в чистую функцию LibraryFilter.
        // Это держит метод тонким и позволяет юнит-тестам фильтра жить без WPF.
        IReadOnlyList<Track>? hits = null;
        if (!string.IsNullOrWhiteSpace(SearchText) && _searchService is not null)
        {
            hits = _searchService.Search(SearchText);
        }

        var criteria = new LibraryFilterCriteria(
            hits,
            SelectedGenre == AllGenres ? null : SelectedGenre,
            MinRating,
            ReactionFilter,
            _selectedTagIds);

        // Адаптер для LibraryFilter: ему нужен делегат «id → список тегов-id»,
        // чтобы не тащить ITagRepository в pure-функцию. Если репозитория нет
        // (старые unit-тесты с null), фильтр по тегам в criteria всё равно
        // не активируется (пустой SelectedTagIds), так что делегат не позовётся.
        Func<int, IReadOnlyList<int>>? tagsProvider = _tagRepository is null
            ? null
            : trackId => _tagRepository.GetTagsForTrack(trackId).Select(t => t.Id).ToList();

        var filtered = LibraryFilter.Apply(_allTracks, criteria, tagsProvider);

        DisplayedTracks.Clear();
        foreach (var track in filtered)
        {
            DisplayedTracks.Add(track);
        }

        // Computed-агрегаты пересчитываются вместе с DisplayedTracks. На 50k треков ~50ms.
        var filteredSnapshot = DisplayedTracks.ToList();
        DisplayedAlbums = LibraryGroupingService.GroupByAlbum(filteredSnapshot);
        DisplayedArtists = LibraryGroupingService.GroupByArtist(filteredSnapshot);
    }

    private void SkipBy(TimeSpan delta)
    {
        TimeSpan target = _audioPlayerService.Position + delta;
        TimeSpan duration = _audioPlayerService.Duration;
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }
        else if (duration > TimeSpan.Zero && target > duration)
        {
            target = duration;
        }

        _audioPlayerService.Position = target;
        CurrentPosition = target;
    }

    private bool CanGoToOffset(int offset)
    {
        if (PlayingTrack is null || DisplayedTracks.Count == 0)
        {
            return false;
        }

        int index = IndexOfPlayingTrack();
        if (index < 0)
        {
            return false;
        }

        int target = index + offset;
        return target >= 0 && target < DisplayedTracks.Count;
    }

    private void GoToTrackByOffset(int offset)
    {
        if (PlayingTrack is null)
        {
            return;
        }

        int index = IndexOfPlayingTrack();
        if (index < 0)
        {
            return;
        }

        int target = index + offset;
        if (target < 0 || target >= DisplayedTracks.Count)
        {
            return;
        }

        Track next = DisplayedTracks[target];
        SelectedTrack = next;
        StartOrResumeTrack(next);
    }

    private int IndexOfPlayingTrack()
    {
        if (PlayingTrack is null)
        {
            return -1;
        }

        for (int i = 0; i < DisplayedTracks.Count; i++)
        {
            if (DisplayedTracks[i].Id == PlayingTrack.Id)
            {
                return i;
            }
        }
        return -1;
    }

    private void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.Current,
            RepeatMode.Current => RepeatMode.Library,
            _ => RepeatMode.Off
        };
    }

    private void PlayOrPause()
    {
        if (SelectedTrack is null)
        {
            SetStatus(OperationResult.Error("Выберите трек."));
            return;
        }

        if (IsSelectedPlaying && IsPlaying)
        {
            _audioPlayerService.Pause();
            _progressTimer.Stop();
            IsPlaying = false;
            _isPaused = true;
            SetStatus(OperationResult.Info("Воспроизведение приостановлено."));
            return;
        }

        StartOrResumeTrack(SelectedTrack);
    }

    private void ReplaceQueueAndPlay(System.Collections.Generic.IReadOnlyList<Track> tracks, bool shuffle)
    {
        var queue = tracks.ToList();
        if (shuffle)
        {
            // Fisher-Yates на копии.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = _shuffleRng.Next(i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
        }

        // Заменяем DisplayedTracks этой очередью — текущая логика воспроизведения
        // ходит по DisplayedTracks (см. PreviousTrackCommand/NextTrackCommand).
        DisplayedTracks.Clear();
        foreach (var t in queue) DisplayedTracks.Add(t);

        SelectedTrack = queue[0];
        PlayPauseCommand.Execute(null);
    }

    private void PlaySpecificTrack(Track? track)
    {
        if (track is null)
        {
            return;
        }

        SelectedTrack = track;
        if (PlayingTrack is not null && PlayingTrack.Id == track.Id && IsPlaying)
        {
            SetStatus(OperationResult.Info("Этот трек уже воспроизводится."));
            return;
        }

        StartOrResumeTrack(track);
    }

    private void ReplayHistoryEntry(PlaybackEntry? entry)
    {
        if (entry?.Track is null)
        {
            return;
        }

        PlaySpecificTrack(entry.Track);
    }

    private void StartOrResumeTrack(Track track)
    {
        // Every playback entry point goes through this method so the Play button,
        // library double-click, and history replay keep identical pause/history rules.
        if (!_fileService.Exists(track.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {track.FilePath}"));
            return;
        }

        bool isResume = _isPaused && PlayingTrack is not null && PlayingTrack.Id == track.Id;

        if (!isResume)
        {
            if (PlayingTrack is not null)
            {
                ResetPlaybackState();
            }

            PlayingTrack = track;
            _pendingHistoryTrack = track;

            OperationResult openResult = _audioPlayerService.Open(track.FilePath);
            if (!openResult.IsSuccess)
            {
                ResetPlaybackState();
                SetStatus(openResult);
                return;
            }
        }

        OperationResult playResult = _audioPlayerService.Play();
        SetStatus(playResult);

        if (playResult.IsSuccess)
        {
            IsPlaying = true;
            _isPaused = false;
            _progressTimer.Start();
            return;
        }

        if (!isResume)
        {
            ResetPlaybackState();
        }
    }

    private void Stop()
    {
        ResetPlaybackState();
        SetStatus(OperationResult.Info("Воспроизведение остановлено."));
    }

    private void ResetPlaybackState()
    {
        _audioPlayerService.Stop();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        PlayingTrack = null;
        _pendingHistoryTrack = null;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
    }

    private void SaveSelectedTrack()
    {
        if (SelectedTrack is null)
        {
            SetStatus(OperationResult.Error("Выберите трек для сохранения."));
            return;
        }

        if (!_fileService.Exists(SelectedTrack.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {SelectedTrack.FilePath}"));
            return;
        }

        string suggestedName = _fileService.GetFileName(SelectedTrack.FilePath);
        string? targetPath = _saveFileDialogService.PickSavePath(suggestedName);
        if (targetPath is null)
        {
            SetStatus(OperationResult.Info("Сохранение отменено."));
            return;
        }

        SetStatus(_fileService.Copy(SelectedTrack.FilePath, targetPath, overwrite: true));
    }

    private void AddToHistory(Track track)
    {
        var entry = new PlaybackEntry { Track = track, PlayedAt = DateTime.Now };
        _listeningHistoryRepository.Append(entry);

        PlaybackHistory.Insert(0, entry);
        while (PlaybackHistory.Count > MaxHistoryItems)
        {
            PlaybackHistory.RemoveAt(PlaybackHistory.Count - 1);
        }
    }

    private void RefreshDuration()
    {
        CurrentDuration = _audioPlayerService.Duration;
    }

    private void HandleMediaOpened(string filePath)
    {
        if (PlayingTrack is null || !IsSamePath(PlayingTrack.FilePath, filePath))
        {
            return;
        }

        RefreshDuration();

        if (_pendingHistoryTrack is not null && IsSamePath(_pendingHistoryTrack.FilePath, filePath))
        {
            AddToHistory(_pendingHistoryTrack);
            _pendingHistoryTrack = null;
        }
    }

    private static bool IsSamePath(string expectedPath, string actualPath)
    {
        // MediaOpened приходит асинхронно, поэтому сверяем путь и отбрасываем устаревшие события.
        return string.Equals(
            Path.GetFullPath(expectedPath),
            Path.GetFullPath(actualPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private void SeekTo(TimeSpan? target)
    {
        if (target is null || PlayingTrack is null)
        {
            return;
        }

        TimeSpan duration = _audioPlayerService.Duration;
        TimeSpan clamped = target.Value;
        if (clamped < TimeSpan.Zero)
        {
            clamped = TimeSpan.Zero;
        }
        else if (duration > TimeSpan.Zero && clamped > duration)
        {
            clamped = duration;
        }

        _audioPlayerService.Position = clamped;
        CurrentPosition = clamped;
    }

    private void RefreshProgress()
    {
        // Пока пользователь тянет ползунок seek-слайдера, не перезаписываем его значение из плеера —
        // иначе позиция будет дёргаться обратно на каждом тике таймера.
        if (_isSeeking)
        {
            return;
        }

        CurrentPosition = _audioPlayerService.Position;
    }

    private void HandleMediaEnded()
    {
        Track? finished = PlayingTrack;

        _audioPlayerService.Stop();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
        _pendingHistoryTrack = null;

        if (finished is null)
        {
            return;
        }

        Track? next = ResolveStrategy(RepeatMode).GetNext(finished, DisplayedTracks);
        if (next is null)
        {
            PlayingTrack = null;
            SetStatus(OperationResult.Info("Воспроизведение завершено."));
            return;
        }

        PlayingTrack = null;
        SelectedTrack = next;
        StartOrResumeTrack(next);
    }

    private static IPlaybackQueueStrategy ResolveStrategy(RepeatMode mode)
    {
        return mode switch
        {
            RepeatMode.Current => RepeatCurrentStrategy.Instance,
            RepeatMode.Library => RepeatLibraryStrategy.Instance,
            _ => NoRepeatStrategy.Instance
        };
    }

    // MediaFailed обрывает цепочку auto-next: иначе на повреждённой библиотеке можно уйти в каскад ошибок.
    private void HandleMediaFailed(string message)
    {
        ResetPlaybackState();
        SetStatus(OperationResult.Error($"Ошибка воспроизведения: {message}"));
    }

    private void SetStatus(OperationResult result)
    {
        StatusMessage = result.Message;
        StatusKind = result.Kind;
    }

    private void PersistSettings()
    {
        _playerSettingsRepository.Save(new PlayerSettings(_volume, _isMuted, _repeatMode));
    }

    public void Dispose()
    {
        _progressTimer.Stop();
        _audioPlayerService.Dispose();
    }
}
