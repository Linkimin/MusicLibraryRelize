# Архитектура MusicBakh

Документ описывает фактическое устройство приложения по состоянию исходного кода версии 1.1.0.

## Обзор

- **Версия:** 1.1.0 (в разработке).
- **Платформа:** WPF, .NET 10 (`net10.0-windows` для Presentation; `net10.0` для остальных слоёв), C# 14, nullable enabled, ImplicitUsings.
- **Проекты:**
  - `MusicBakh.Core` — доменные сущности и абстракции репозиториев.
  - `MusicBakh.Application` — интерфейсы сервисов уровня use-case (аудио, импорт, файлы, метаданные, обложки).
  - `MusicBakh.Infrastructure` — реализации: SQLite через EF Core, HTTP-клиенты, файловая система, сидер.
  - `MusicLibrary` — WPF-хост: ViewModels, Views, WPF-привязанные реализации (`MediaPlayerAudioService`, `ProceduralCoverGenerator`), DI-бутстрап.
- **Ключевые зависимости:**
  - `Microsoft.EntityFrameworkCore.Sqlite 10.0.8` — ORM и SQLite-провайдер.
  - `Microsoft.Extensions.Hosting 10.0.x` — DI-контейнер и host-инфраструктура.
  - `TagLibSharp 2.3.0` — чтение ID3-тегов.
  - `System.Windows.Media.MediaPlayer` (BCL WPF) — воспроизведение аудио.

## Слои и зависимости

```
┌──────────────────────────────────────┐
│  MusicLibrary  (Presentation / WPF)  │
│  ViewModels, Views, App.OnStartup    │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
┌──────────────────────────────────────┐
│  MusicBakh.Application               │
│  Интерфейсы сервисов use-case        │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
┌──────────────────────────────────────┐
│  MusicBakh.Core                      │
│  Доменные сущности, интерфейсы       │
│  репозиториев                        │
└──────────────────────────────────────┘

┌──────────────────────────────────────┐
│  MusicBakh.Infrastructure            │
│  SQLite-репозитории, HTTP-клиенты,   │
│  файловая система, сидер, миграция   │
└──────────────┬───────────────────────┘
               │ ссылается на
               ▼
    MusicBakh.Application + MusicBakh.Core
```

Presentation и Infrastructure — два независимых конкретных слоя. Оба ссылаются на Application/Core, но друг на друга не ссылаются. Зависимости между конкретными реализациями разрешаются только через DI-контейнер в `App.OnStartup`.

## Каталоги проекта

```
MusicBakh.Core/
├── Domain/
│   ├── Track.cs                        — доменная модель трека
│   ├── PlaybackEntry.cs                — запись истории воспроизведения
│   ├── PlayerSettings.cs               — громкость, mute, режим повтора
│   ├── RepeatMode.cs                   — enum (NoRepeat/Current/Library)
│   └── OperationResult.cs              — результат операций UI
└── Abstractions/
    ├── ITrackRepository.cs             — CRUD-репозиторий треков
    ├── IListeningHistoryRepository.cs  — хранилище истории
    ├── IPlayerSettingsRepository.cs    — сохранение настроек плеера
    └── IClock.cs                       — абстракция системного времени

MusicBakh.Application/
├── Abstractions/                       — интерфейсы сервисов use-case
│   ├── IAudioPlayerService.cs
│   ├── ITrackImporter.cs
│   ├── IMetadataResolver.cs
│   ├── ICoverResolver.cs
│   ├── ITagReader.cs
│   ├── IMusicBrainzClient.cs
│   ├── IItunesCoverClient.cs
│   ├── IGenreNormalizer.cs
│   ├── IProceduralCoverGenerator.cs
│   ├── IFileService.cs
│   ├── IOpenFileDialogService.cs
│   ├── ISaveFileDialogService.cs
│   ├── IConfirmationService.cs
│   ├── IPlaybackQueueStrategy.cs
│   └── IMusicStoragePaths.cs
└── Contracts/                          — DTO для пайплайна импорта
    ├── ImportRequest.cs                — LocalFileImportRequest / UrlImportRequest
    ├── ImportResult.cs
    ├── TrackImportCandidate.cs
    ├── ResolvedMetadata.cs
    ├── ResolvedCover.cs
    ├── LocalTagInfo.cs
    ├── MusicBrainzMatch.cs
    └── ItunesSearchHit.cs

MusicBakh.Infrastructure/
├── Persistence/
│   ├── LibraryDbContext.cs             — EF Core DbContext
│   ├── LibraryDbContextOptions.cs      — пути к SQLite-файлу
│   ├── Entities/                       — EF-сущности (TrackEntity, ListeningHistoryEntryEntity, KeyValueEntryEntity)
│   ├── Configurations/                 — IEntityTypeConfiguration для каждой таблицы
│   ├── Repositories/                   — SqliteTrackRepository, SqliteListeningHistoryRepository, SqlitePlayerSettingsRepository
│   └── Migrations/                     — AddLibrarySchema, AddListeningHistory, AddKeyValueStore
├── Migration/
│   └── JsonToSqliteMigrationService.cs — перенос userTracks.json → SQLite
├── Seeding/
│   └── BuiltInTrackSeeder.cs           — наполнение встроенными треками при первом запуске
├── Import/                             — TrackImporter
├── Metadata/                           — DefaultMetadataResolver, TagLibSharpTagReader, MusicBrainzClient, RussianGenreNormalizer
├── Covers/                             — CompositeCoverResolver, ItunesCoverClient
├── FileSystem/                         — FileService, MusicStoragePaths
├── Playback/                           — стратегии очереди (NoRepeat/RepeatCurrent/RepeatLibrary)
├── Time/                               — SystemClock
└── DependencyInjection/
    └── InfrastructureServiceCollectionExtensions.cs   — AddMusicBakhInfrastructure()

MusicLibrary/
├── App.xaml(.cs)                       — DI-бутстрап, AppUserModelID, OnStartup
├── MainWindow.xaml(.cs)                — обработчики seek-слайдера, NativeWindowAppearance
├── NativeWindowAppearance.cs           — DWM dark caption/border/text color
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs                — главный VM, все ICommand
│   └── AddTrackViewModel.cs
├── Views/
│   ├── AddTrackWindow.xaml(.cs)
│   ├── ConfirmationDialogWindow.xaml(.cs)
│   └── ConfirmationDialogService.cs
├── Services/
│   ├── Playback/MediaPlayerAudioService.cs   — WPF MediaPlayer
│   ├── Covers/ProceduralCoverGenerator.cs    — градиентная заглушка обложки
│   ├── Storage/                              — устаревшие JSON-адаптеры (не используются в 1.1.0)
│   ├── Tracks/CompositeTrackRepository.cs    — объединяет встроенные и пользовательские
│   └── Files/                               — OpenFileDialogService, SaveFileDialogService
├── Commands/RelayCommand.cs
├── Converters/                         — IValueConverter для XAML
├── DependencyInjection/
│   └── PresentationServiceCollectionExtensions.cs   — AddMusicBakhPresentation()
├── Resources/                          — XAML словари (Colors, Brushes, стили, иконки)
├── Assets/Brand/                       — musicbakh.ico, musicbakh-logo.png
├── Music/                              — три эталонных mp3 (копируются в output)
└── Covers/                             — обложки к эталонным трекам

MusicLibrary.Tests/
├── Persistence/                        — SqliteTrackRepositoryTests, SqliteListeningHistoryRepositoryTests, SqlitePlayerSettingsRepositoryTests
├── Migration/                          — JsonToSqliteMigrationServiceTests
├── Seeding/                            — тесты BuiltInTrackSeeder
├── TestSupport/
│   └── InMemorySqliteDbContextFactory.cs   — in-memory SQLite для тестов
└── *.cs                                — тесты MainViewModel, сервисов, конвертеров
```

## Хранилище данных

| Путь | Содержимое |
|---|---|
| `%LocalAppData%\MusicLibrary\library.db` | SQLite-база: треки, история воспроизведения, настройки плеера |
| `%LocalAppData%\MusicLibrary\Music\` | Импортированные аудиофайлы (mp3/wav) |
| `%LocalAppData%\MusicLibrary\Covers\` | Обложки с GUID в имени (`{uuid}.{ext}`) |
| `%LocalAppData%\MusicLibrary\userTracks.json.backup-<timestamp>` | Резервная копия legacy-файла; появляется только после первого запуска при апгрейде с 1.0.x |

Эталонные 3 трека и их обложки лежат рядом с `.exe` (`{app}\Music\`, `{app}\Covers\`) — они read-only и удалить их через UI нельзя (флаг `IsBuiltIn`).

SQLite-файл создаётся автоматически при первом запуске через `Database.Migrate()`. Каталоги `Music\` и `Covers\` создаются сервисом `MusicStoragePaths` по требованию.

## EF Core миграции

Миграции находятся в [`MusicBakh.Infrastructure/Persistence/Migrations/`](../MusicBakh.Infrastructure/Persistence/Migrations/). `Database.Migrate()` применяет их при каждом запуске приложения идемпотентно.

| Миграция | Что добавляет |
|---|---|
| `AddLibrarySchema` | Таблица `Tracks`: Id, Title, Artist, Genre, DurationSeconds, FilePath, CoverPath, IsBuiltIn |
| `AddListeningHistory` | Таблица `ListeningHistory`: Id, TrackId, PlayedAt (UTC) |
| `AddKeyValueStore` | Таблица `KeyValueStore`: Key (PK), Value — используется для хранения настроек плеера |

## DI-композиция

Точка входа — [`MusicLibrary/App.xaml.cs`](../MusicLibrary/App.xaml.cs), метод `OnStartup`:

1. **Сборка host:** `Host.CreateDefaultBuilder()` с двумя расширениями:
   - `services.AddMusicBakhInfrastructure(LibraryDbContextOptions.Default)` — регистрирует `LibraryDbContext`, все SQLite-репозитории, HTTP-клиенты, файловые сервисы, сидер, `JsonToSqliteMigrationService`.
   - `services.AddMusicBakhPresentation()` — регистрирует ViewModels, `MainWindow`, WPF-сервисы (`MediaPlayerAudioService`, `ProceduralCoverGenerator`, диалоги).
2. **Инициализация БД** (`InitializeDatabase`):
   - `LibraryDbContext.Database.Migrate()` — создаёт SQLite-файл и применяет миграции.
   - `JsonToSqliteMigrationService.Run()` — переносит legacy `userTracks.json` в таблицу `Tracks` (идемпотентно).
   - `BuiltInTrackSeeder.SeedIfEmpty()` — заполняет встроенные треки, если таблица пуста.
3. **Открытие окна:** `_host.Services.GetRequiredService<MainWindow>().Show()`.

## Миграция с 1.0.0

[`JsonToSqliteMigrationService`](../MusicBakh.Infrastructure/Migration/JsonToSqliteMigrationService.cs) обеспечивает бесшовный апгрейд:

1. Проверяет наличие `%LocalAppData%\MusicLibrary\userTracks.json`.
2. Если файл найден — читает список `UserTrack`, вставляет записи в таблицу `Tracks` через `SqliteTrackRepository`.
3. Переименовывает исходный файл в `userTracks.json.backup-yyyyMMdd-HHmmss`.

После переименования повторный запуск приложения не находит JSON-файла и пропускает миграцию — операция идемпотентна. Резервный файл можно удалить вручную в любой момент.

## Слой ViewModel

[`MainViewModel`](../MusicLibrary/ViewModels/MainViewModel.cs) — центральный класс, держит:

- `ObservableCollection<Track> DisplayedTracks` — отфильтрованный список для UI.
- `ObservableCollection<PlaybackEntry> PlaybackHistory` — последние 50 запусков.
- Раздельные `SelectedTrack` (выделение в списке) и `PlayingTrack` (фактически играет). См. п. 7 в scope-deviations.
- `DispatcherTimer` (500 мс) обновляет `CurrentPosition` пока трек играет, кроме момента когда `IsSeeking = true`.

Команды (все `ICommand`):

| Команда | Описание |
|---|---|
| `PlayPauseCommand` | Старт/пауза, активна при `SelectedTrack != null` |
| `StopCommand` | Стоп, активна при `PlayingTrack != null` |
| `SaveTrackCommand` | Экспорт выбранного трека через `SaveFileDialog` |
| `AddTrackCommand` | Открывает окно импорта |
| `DeleteTrackCommand` | Удаление пользовательского трека (с подтверждением) |
| `PlayTrackCommand` | Запуск конкретного трека (double-click) |
| `ReplayHistoryEntryCommand` | Повтор трека из истории |
| `SkipForwardCommand` / `SkipBackwardCommand` | Перемотка ±10 с |
| `PreviousTrackCommand` / `NextTrackCommand` | Переход по `DisplayedTracks` |
| `ToggleMuteCommand` | Mute on/off |
| `CycleRepeatModeCommand` | NoRepeat → Current → Library → … |
| `SeekToCommand` | Перемотка на конкретную позицию |

## Пайплайн импорта

```
[Кнопка «+ Добавить трек»]
        │
        ▼
AddTrackWindow ─── выбор источника ─── LocalFileImportRequest | UrlImportRequest
        │
        ▼
TrackImporter.ImportAsync()
        ├── скачать/скопировать файл в %LocalAppData%\MusicLibrary\Music\
        ├── DefaultMetadataResolver.ResolveAsync()
        │       ├── TagLibSharpTagReader.Read()
        │       ├── StripBrandSuffix() против чёрного списка агрегаторов
        │       ├── MusicBrainzClient.SearchAsync() (throttle 1.1 с)
        │       └── RussianGenreNormalizer.Normalize()
        ├── CompositeCoverResolver.ResolveAsync()
        │       ├── APIC из ID3
        │       ├── ItunesCoverClient.FindAsync() (600×600)
        │       └── ProceduralCoverGenerator.Generate() (512×512 градиент)
        ▼
TrackImportCandidate ─── редактирование пользователем ─── SqliteTrackRepository.AddAsync()
```

Все HTTP-вызовы (MusicBrainz и iTunes) защищены per-request таймаутами по 10 секунд через `CancellationTokenSource`.

## Пайплайн воспроизведения

```
SelectedTrack ─── PlayPauseCommand ──▶ MediaPlayerAudioService.Open(filePath)
                                              │
                                              ▼
                                       MediaPlayer (System.Windows.Media)
                                              │
                                       ┌──────┴──────┬─────────────┐
                                       ▼             ▼             ▼
                                  MediaOpened   MediaEnded    MediaFailed
                                       │             │             │
                                       │     IPlaybackQueueStrategy │
                                       │             │             │
                                       ▼             ▼             ▼
                                  CurrentDuration  Auto-next   StatusMessage
                                  устанавливается  по режиму   с ошибкой
                                                   повтора
```

`DispatcherTimer` тикает каждые 500 мс и пишет `MediaPlayer.Position` в `CurrentPosition` ViewModel. Таймер не трогает значение пока пользователь тащит seek-слайдер (`IsSeeking == true`).

## Тестирование

Единственный тестовый проект — `MusicLibrary.Tests` (xUnit). По состоянию версии 1.1.0-A: **124 теста**.

- **SQLite-репозитории** (`SqliteTrackRepositoryTests`, `SqliteListeningHistoryRepositoryTests`, `SqlitePlayerSettingsRepositoryTests`) — работают поверх in-memory SQLite через [`InMemorySqliteDbContextFactory`](../MusicLibrary.Tests/TestSupport/InMemorySqliteDbContextFactory.cs).
- **Миграция** (`JsonToSqliteMigrationServiceTests`) — проверяет перенос записей из JSON в SQLite и переименование файла.
- **ViewModel** (`MainViewModelTests`) — покрывает логику `MainViewModel` через test doubles (моки репозиториев и сервисов), без поднятия WPF.
- **Сервисы** — `DefaultMetadataResolverTests`, `GenreNormalizerTests`, `PlaybackQueueStrategyTests`, `CompositeTrackRepositoryTests`, `FileServiceTests` и др.

## Внешний вид окна

[`NativeWindowAppearance.cs`](../MusicLibrary/NativeWindowAppearance.cs) через DWM API красит нативный caption-bar Windows в фирменный тёмно-фиолетовый (`#16161F`) и выставляет светлый текст заголовка. Применяется в `SourceInitialized` главного окна и `ConfirmationDialogWindow`.

## Ресурсы и стили

Все XAML-словари лежат в `MusicLibrary/Resources/` и подключаются в [`App.xaml`](../MusicLibrary/App.xaml):

- `Colors.xaml`, `Brushes.xaml` — палитра.
- `ButtonStyles.xaml`, `ComboBoxStyles.xaml`, `SliderStyles.xaml`, `ScrollBarStyles.xaml`, `ListStyles.xaml` — переопределение системных контролов.
- `PlayerIcons.xaml` — векторные иконки play/pause/skip/repeat/volume через `Geometry`.
- `TrackTemplates.xaml` — `DataTemplate` для карточки трека в `ListBox`.

## Сборка

См. [release-checklist.md](release-checklist.md). Краткий тех-список: `net10.0-windows`, `WinExe`, `UseWPF=true`, в Release-конфигурации `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `EnableCompressionInSingleFile=true`, `PublishReadyToRun=false`.
