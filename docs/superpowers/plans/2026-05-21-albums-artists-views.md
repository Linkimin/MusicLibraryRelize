# Albums and Artists Views — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать спек [`docs/superpowers/specs/2026-05-21-albums-artists-views.md`](../specs/2026-05-21-albums-artists-views.md) — три tabs «Треки/Альбомы/Исполнители» в шапке приложения, drill-down детальные страницы в левой колонке, computed-агрегаты `AlbumAggregate`/`ArtistAggregate`, поддержка compilations через `AlbumArtist`.

**Architecture:** Доменная модель `Track` расширяется тремя nullable-полями (`Year`, `TrackNumber`, `AlbumArtist`). Группировка реализуется как pure-функции `LibraryGroupingService.GroupByAlbum`/`GroupByArtist` поверх отфильтрованного `LibraryFilter`-выхода. UI-state — `MainViewMode` enum + back-стек `LeftColumnState`-узлов в `MainViewModel`. XAML рендерит ContentControl с `DataTemplateSelector`, переключающим шаблон по текущему `LeftColumnState`.

**Tech Stack:** .NET 10, WPF, C# 14, EF Core 10 SQLite, xUnit. Никаких новых NuGet-зависимостей.

**TDD adaptation:** Pure-функции (`LibraryGroupingService`, аггрегаты, ViewModel-логика) и репозитории тестируются классически: red → green → refactor. XAML-задачи unit-тестам не поддаются — для них цикл «изменение → build → smoke-запуск → визуальная проверка → commit».

---

## Карта файлов

```
MusicBakh.Core/
├── Domain/
│   └── Track.cs                                       (изменяется: + Year, TrackNumber, AlbumArtist)

MusicBakh.Application/
├── Contracts/
│   ├── LocalTagInfo.cs                                (изменяется: + 3 поля)
│   ├── ResolvedMetadata.cs                            (изменяется: + 3 поля)
│   └── TrackImportCandidate.cs                        (изменяется: + 3 поля)

MusicBakh.Infrastructure/
├── Persistence/
│   ├── Entities/TrackEntity.cs                        (изменяется: + 3 поля)
│   ├── Configurations/TrackEntityConfiguration.cs     (изменяется: + Year индекс)
│   ├── Migrations/
│   │   └── 2026XXXX_AddTrackYearNumberAlbumArtist.cs (NEW)
│   └── Repositories/
│       ├── SqliteTrackRepository.cs                   (изменяется: маппинги в обе стороны)
│       └── SqliteListeningHistoryRepository.cs        (изменяется: MapEntityToTrack)
├── Search/
│   └── SqliteFtsSearchService.cs                      (изменяется: MapToDomain)
├── Metadata/
│   ├── TagLibSharpTagReader.cs                        (изменяется: + Year, Track, FirstAlbumArtist)
│   └── DefaultMetadataResolver.cs                     (изменяется: проброс полей)
└── Import/
    └── TrackImporter.cs                               (изменяется: проброс полей)

MusicLibrary/
├── Services/Library/
│   ├── AlbumAggregate.cs                              (NEW record)
│   ├── ArtistAggregate.cs                             (NEW record)
│   ├── LibraryGroupingService.cs                      (NEW static class: GroupByAlbum, GroupByArtist)
│   ├── LeftColumnState.cs                             (NEW: discriminated union of view-states)
│   └── MainViewMode.cs                                (NEW: enum Tracks/Albums/Artists)
├── ViewModels/
│   ├── MainViewModel.cs                               (изменяется: ActiveView, nav-стек, агрегаты,
│   │                                                   SwitchViewCommand, OpenAlbum/Artist, Back,
│   │                                                   Play/Shuffle commands)
│   └── (никаких новых VM — детальные view рендерятся теми же командами через DataTemplate)
├── Selectors/
│   └── LeftColumnTemplateSelector.cs                  (NEW: переключает DataTemplate по LeftColumnState)
├── Resources/
│   ├── AlbumsArtistsTemplates.xaml                    (NEW: AlbumTile, ArtistRow, AlbumDetail, ArtistDetail)
│   └── MainViewTabsStyles.xaml                        (NEW: стиль табов в шапке)
├── MainWindow.xaml                                    (изменяется: tabs в шапке, ContentControl
│                                                       с TemplateSelector в левой колонке)
└── MainWindow.xaml.cs                                 (изменяется: Ctrl+1/2/3, Esc-back logic)

MusicLibrary.Tests/
├── Library/
│   └── LibraryGroupingServiceTests.cs                 (NEW, ~12 тестов)
├── Persistence/
│   └── SqliteTrackRepositoryTests.cs                  (изменяется: + Year/TrackNumber/AlbumArtist
│                                                       round-trip)
├── ViewModels/
│   └── MainViewModelNavigationTests.cs                (NEW, ~6 тестов на SwitchView + back-стек)
├── DefaultMetadataResolverTests.cs                    (изменяется: проброс 3 полей)
└── Migrations/
    └── MigrationBackfillTests.cs                      (изменяется: MigrateUpToHead снова проходит)
```

---

## Task 1 — Доменное расширение Track (Year / TrackNumber / AlbumArtist) + миграция + маппинги + тесты

Самый базовый шаг: добавить три новых nullable-поля в Track во всех слоях, мигрировать БД, протащить через все три репозитория и FTS-сервис.

**Files:**
- Modify: `MusicBakh.Core/Domain/Track.cs`
- Modify: `MusicBakh.Infrastructure/Persistence/Entities/TrackEntity.cs`
- Modify: `MusicBakh.Infrastructure/Persistence/Configurations/TrackEntityConfiguration.cs`
- Modify: `MusicBakh.Infrastructure/Persistence/Repositories/SqliteTrackRepository.cs`
- Modify: `MusicBakh.Infrastructure/Persistence/Repositories/SqliteListeningHistoryRepository.cs`
- Modify: `MusicBakh.Infrastructure/Search/SqliteFtsSearchService.cs`
- Create: `MusicBakh.Infrastructure/Persistence/Migrations/<timestamp>_AddTrackYearNumberAlbumArtist.cs`
- Modify: `MusicLibrary.Tests/Persistence/SqliteTrackRepositoryTests.cs`

### Steps

- [ ] **1.1 — Добавить поля в `Track`**

Files: `MusicBakh.Core/Domain/Track.cs`

После существующего `public TrackReaction Reaction { get; init; }` добавить:
```csharp
/// <summary>Год выпуска альбома, опционально (из ID3-тега).</summary>
public int? Year { get; init; }

/// <summary>Позиция трека в альбоме (1-based), опционально (из ID3-тега).</summary>
public int? TrackNumber { get; init; }

/// <summary>Исполнитель альбома — отличается от Artist для compilations (например, «Various Artists»).</summary>
public string? AlbumArtist { get; init; }
```

- [ ] **1.2 — Добавить поля в `TrackEntity`**

Files: `MusicBakh.Infrastructure/Persistence/Entities/TrackEntity.cs`

После существующего `public bool IsBuiltIn { get; set; }` добавить:
```csharp
public int? Year { get; set; }
public int? TrackNumber { get; set; }
public string? AlbumArtist { get; set; }
```

- [ ] **1.3 — Добавить EF-конфигурацию**

Files: `MusicBakh.Infrastructure/Persistence/Configurations/TrackEntityConfiguration.cs`

Найти секцию `// Индексы.` и добавить перед `builder.HasIndex(t => t.IsBuiltIn);`:
```csharp
builder.Property(t => t.AlbumArtist).HasMaxLength(500);
builder.HasIndex(t => t.Year);
```

- [ ] **1.4 — Обновить маппинги в `SqliteTrackRepository`**

Files: `MusicBakh.Infrastructure/Persistence/Repositories/SqliteTrackRepository.cs`

В методе `Add(Track track)` после `Reaction = (int)track.Reaction,` добавить:
```csharp
Year = track.Year,
TrackNumber = track.TrackNumber,
AlbumArtist = track.AlbumArtist,
```

В методе `Update(Track track)` в блоке копирования полей сущности после `entity.Reaction = (int)track.Reaction;` добавить:
```csharp
entity.Year = track.Year;
entity.TrackNumber = track.TrackNumber;
entity.AlbumArtist = track.AlbumArtist;
```

В private static `MapToDomain` после `Reaction = (TrackReaction)e.Reaction,` добавить:
```csharp
Year = e.Year,
TrackNumber = e.TrackNumber,
AlbumArtist = e.AlbumArtist,
```

- [ ] **1.5 — Обновить маппинг в `SqliteListeningHistoryRepository`**

Files: `MusicBakh.Infrastructure/Persistence/Repositories/SqliteListeningHistoryRepository.cs`

В `private static Track MapEntityToTrack(TrackEntity e)` после `Reaction = (TrackReaction)e.Reaction,` добавить:
```csharp
Year = e.Year,
TrackNumber = e.TrackNumber,
AlbumArtist = e.AlbumArtist,
```

- [ ] **1.6 — Обновить маппинг в `SqliteFtsSearchService`**

Files: `MusicBakh.Infrastructure/Search/SqliteFtsSearchService.cs`

В `private static Track MapToDomain(TrackEntity e)` после `Reaction = (TrackReaction)e.Reaction,` добавить:
```csharp
Year = e.Year,
TrackNumber = e.TrackNumber,
AlbumArtist = e.AlbumArtist,
```

- [ ] **1.7 — Сгенерировать EF-миграцию**

Run: `dotnet ef migrations add AddTrackYearNumberAlbumArtist -p MusicBakh.Infrastructure -s MusicBakh.Infrastructure`

Откроется новый файл `MusicBakh.Infrastructure/Persistence/Migrations/<timestamp>_AddTrackYearNumberAlbumArtist.cs`. Применить **обязательный alias** (см. `CLAUDE.md`):

Заменить
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackYearNumberAlbumArtist : Migration
```

на
```csharp
using Microsoft.EntityFrameworkCore.Migrations;
using EFMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace MusicBakh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackYearNumberAlbumArtist : EFMigration
```

Сгенерированный `Up()` должен содержать `AddColumn` для трёх полей + `CreateIndex` для `Year`. Если не содержит — проверь, что Step 1.3 применился корректно.

- [ ] **1.8 — Написать failing-тест round-trip в `SqliteTrackRepositoryTests`**

Files: `MusicLibrary.Tests/Persistence/SqliteTrackRepositoryTests.cs`

Перед закрывающей `}` класса добавить:
```csharp
[Fact]
public void Add_Then_FindById_Preserves_Year_TrackNumber_AlbumArtist()
{
    using var factory = new InMemorySqliteDbContextFactory();
    var repo = new SqliteTrackRepository(factory.CreateContext);

    var saved = repo.Add(new Track
    {
        Title = "Night Witches",
        Artist = "Sabaton",
        Album = "Heroes",
        FilePath = "1.mp3",
        Year = 2014,
        TrackNumber = 1,
        AlbumArtist = "Sabaton"
    });

    var loaded = repo.FindById(saved.Id);
    Assert.NotNull(loaded);
    Assert.Equal(2014, loaded!.Year);
    Assert.Equal(1, loaded.TrackNumber);
    Assert.Equal("Sabaton", loaded.AlbumArtist);
}

[Fact]
public void Add_With_Null_New_Fields_Defaults_To_Null()
{
    using var factory = new InMemorySqliteDbContextFactory();
    var repo = new SqliteTrackRepository(factory.CreateContext);

    var saved = repo.Add(new Track { Title = "X", Artist = "Y", FilePath = "1.mp3" });

    var loaded = repo.FindById(saved.Id);
    Assert.Null(loaded!.Year);
    Assert.Null(loaded.TrackNumber);
    Assert.Null(loaded.AlbumArtist);
}
```

- [ ] **1.9 — Запустить тесты**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 215 passed (213 + 2 новых).

- [ ] **1.10 — Коммит**

```bash
git add MusicBakh.Core MusicBakh.Application MusicBakh.Infrastructure MusicLibrary.Tests
git commit -m "$(cat <<'EOF'
feat(domain): Track gets Year + TrackNumber + AlbumArtist (nullable)

Three new nullable fields to support album/artist views in v1.0.4:
* Year (int?): release year, read from ID3 tag, used for sorting albums.
* TrackNumber (int?): position in album, used for sorting tracks inside
  an album detail view.
* AlbumArtist (string?): for compilations — when present, becomes the
  grouping key for albums (Track.Artist still drives the Artists view).

Migration AddTrackYearNumberAlbumArtist adds three nullable columns and
an index on Year. Existing rows get NULL until next re-import.

All three repositories (SqliteTrackRepository, SqliteListeningHistoryRepository,
SqliteFtsSearchService) propagate the fields in both directions. Two
round-trip tests verify Add → FindById preserves the values and that
omitted fields default to NULL. 215 tests green.
EOF
)"
```

---

## Task 2 — Импорт-pipeline пробрасывает 3 новых поля из ID3

Цепочка чтения ID3 → доменный объект → БД должна теперь нести `Year`/`TrackNumber`/`AlbumArtist`.

**Files:**
- Modify: `MusicBakh.Application/Contracts/LocalTagInfo.cs`
- Modify: `MusicBakh.Application/Contracts/ResolvedMetadata.cs`
- Modify: `MusicBakh.Application/Contracts/TrackImportCandidate.cs`
- Modify: `MusicBakh.Infrastructure/Metadata/TagLibSharpTagReader.cs`
- Modify: `MusicBakh.Infrastructure/Metadata/DefaultMetadataResolver.cs`
- Modify: `MusicBakh.Infrastructure/Import/TrackImporter.cs`
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs` (в `OpenAddTrackDialog` при создании Track для `_trackRepository.Add(...)`)
- Modify: `MusicLibrary.Tests/DefaultMetadataResolverTests.cs`

### Steps

- [ ] **2.1 — Добавить поля в `LocalTagInfo`**

Files: `MusicBakh.Application/Contracts/LocalTagInfo.cs`

После последнего property:
```csharp
public int? Year { get; init; }
public int? TrackNumber { get; init; }
public string? AlbumArtist { get; init; }
```

- [ ] **2.2 — Добавить поля в `ResolvedMetadata`**

Files: `MusicBakh.Application/Contracts/ResolvedMetadata.cs`

Те же три property — аналогично Step 2.1.

- [ ] **2.3 — Добавить поля в `TrackImportCandidate`**

Files: `MusicBakh.Application/Contracts/TrackImportCandidate.cs`

Те же три property.

- [ ] **2.4 — Прочитать поля в `TagLibSharpTagReader`**

Files: `MusicBakh.Infrastructure/Metadata/TagLibSharpTagReader.cs`

В методе `Read(string filePath)` после блока `string album = ...;`:
```csharp
uint year = file.Tag.Year;
uint trackNumber = file.Tag.Track;
string? albumArtist = file.Tag.FirstAlbumArtist;
if (string.IsNullOrWhiteSpace(albumArtist))
{
    albumArtist = null;
}
```

В блоке возврата `new LocalTagInfo { ... }` после `Album = album,` (или `Artist = ...,` — куда удобнее) добавить:
```csharp
Year = year > 0 ? (int)year : (int?)null,
TrackNumber = trackNumber > 0 ? (int)trackNumber : (int?)null,
AlbumArtist = albumArtist,
```

(`uint = 0` у TagLib# означает «тег отсутствует» — оба `Year` и `Track`. См. документацию TagLibSharp.)

- [ ] **2.5 — Пробросить через `DefaultMetadataResolver`**

Files: `MusicBakh.Infrastructure/Metadata/DefaultMetadataResolver.cs`

В методе `ResolveAsync(...)`, в финальный `return new ResolvedMetadata { ... }` после `Album = albumNormalized,` добавить:
```csharp
Year = tagInfo.Year,
TrackNumber = tagInfo.TrackNumber,
AlbumArtist = tagInfo.AlbumArtist,
```

- [ ] **2.6 — Пробросить через `TrackImporter`**

Files: `MusicBakh.Infrastructure/Import/TrackImporter.cs`

В `Import(...)` (или эквивалентном) при создании `new TrackImportCandidate { ... }` после `Album = metadata.Album,` добавить:
```csharp
Year = metadata.Year,
TrackNumber = metadata.TrackNumber,
AlbumArtist = metadata.AlbumArtist,
```

- [ ] **2.7 — Пробросить из `MainViewModel.OpenAddTrackDialog` в `_trackRepository.Add`**

Files: `MusicLibrary/ViewModels/MainViewModel.cs`

Найти метод `OpenAddTrackDialog` (или эквивалентный — где `_trackRepository.Add(new Track { ... })` вызывается). В создании `new Track { ... }` после `Album = candidate.Album,` (или `Genre = candidate.Genre,`) добавить:
```csharp
Year = candidate.Year,
TrackNumber = candidate.TrackNumber,
AlbumArtist = candidate.AlbumArtist,
```

- [ ] **2.8 — Тест на проброс**

Files: `MusicLibrary.Tests/DefaultMetadataResolverTests.cs`

Перед закрывающей `}` класса:
```csharp
[Fact]
public async Task ResolveAsync_Propagates_Year_TrackNumber_AlbumArtist()
{
    var tagInfo = new LocalTagInfo
    {
        Title = "Night Witches",
        Artist = "Sabaton",
        Album = "Heroes",
        Year = 2014,
        TrackNumber = 1,
        AlbumArtist = "Sabaton"
    };
    var tagReader = new FakeTagReader(tagInfo);
    var brainz = new FakeBrainz(null);
    var itunes = new FakeItunes(null);
    var resolver = new DefaultMetadataResolver(tagReader, brainz, itunes, new FakeNormalizer());

    var result = await resolver.ResolveAsync("any.mp3", default);

    Assert.Equal(2014, result.Year);
    Assert.Equal(1, result.TrackNumber);
    Assert.Equal("Sabaton", result.AlbumArtist);
}
```

(Если `FakeTagReader`/`FakeBrainz`/`FakeItunes`/`FakeNormalizer` в этом тестовом файле уже есть с другими именами — переиспользуй их сигнатуры. Если их нет — это `DefaultMetadataResolverTests`, шумные fakes должны быть рядом; используй их фактические имена.)

- [ ] **2.9 — Запустить тесты**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 216 passed (215 + 1 новый).

- [ ] **2.10 — Smoke-запуск приложения**

Run: `cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: EF-логи без exception. Реальный импорт треков работает (можно проверить руками — импортнуть mp3, посмотреть в БД через DB Browser, что Year/TrackNumber/AlbumArtist заполнились).

- [ ] **2.11 — Коммит**

```bash
git add MusicBakh.Application MusicBakh.Infrastructure MusicLibrary MusicLibrary.Tests
git commit -m "feat(import): propagate Year/TrackNumber/AlbumArtist through import pipeline

ID3 tags читаются TagLibSharpTagReader-ом (file.Tag.Year / .Track /
.FirstAlbumArtist), пробрасываются через LocalTagInfo, ResolvedMetadata,
TrackImportCandidate. MainViewModel.OpenAddTrackDialog сохраняет
их в БД при создании Track-а.

Year/Track=0 в TagLibSharp семантически означает «тег отсутствует»,
маппим в C# null. AlbumArtist=пустая строка тоже маппим в null."
```

---

## Task 3 — `LibraryGroupingService` + аггрегаты + 12 тестов

Pure-функция группировки. Полностью изолирована, на 100% TDD.

**Files:**
- Create: `MusicLibrary/Services/Library/AlbumAggregate.cs`
- Create: `MusicLibrary/Services/Library/ArtistAggregate.cs`
- Create: `MusicLibrary/Services/Library/LibraryGroupingService.cs`
- Create: `MusicLibrary.Tests/Library/LibraryGroupingServiceTests.cs`

### Steps

- [ ] **3.1 — Создать `AlbumAggregate`**

Files: `MusicLibrary/Services/Library/AlbumAggregate.cs`

```csharp
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Агрегат альбома (computed из треков, не persisted). Identity = (Artist, Title).
/// </summary>
public sealed record AlbumAggregate(
    string Title,
    string Artist,
    int? Year,
    string CoverPath,
    System.Collections.Generic.IReadOnlyList<Track> Tracks)
{
    public string AlbumKey => Artist + " " + Title;
    public System.TimeSpan TotalDuration
    {
        get
        {
            var sum = System.TimeSpan.Zero;
            foreach (var t in Tracks) sum += t.Duration;
            return sum;
        }
    }
}
```

- [ ] **3.2 — Создать `ArtistAggregate`**

Files: `MusicLibrary/Services/Library/ArtistAggregate.cs`

```csharp
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Агрегат исполнителя (computed). Identity = Name.
/// </summary>
public sealed record ArtistAggregate(
    string Name,
    System.Collections.Generic.IReadOnlyList<AlbumAggregate> Albums,
    System.Collections.Generic.IReadOnlyList<Track> LooseTracks,
    int TotalTracks,
    System.TimeSpan TotalDuration);
```

- [ ] **3.3 — Тесты `LibraryGroupingServiceTests` (failing — RED)**

Files: `MusicLibrary.Tests/Library/LibraryGroupingServiceTests.cs`

```csharp
using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using Xunit;

namespace MusicLibrary.Tests.Library;

public sealed class LibraryGroupingServiceTests
{
    private static Track T(int id, string title, string artist, string album, int? year = null, int? trackNo = null, string? albumArtist = null, int durationSec = 180, string coverPath = "")
        => new()
        {
            Id = id,
            Title = title,
            Artist = artist,
            Album = album,
            Year = year,
            TrackNumber = trackNo,
            AlbumArtist = albumArtist,
            Duration = System.TimeSpan.FromSeconds(durationSec),
            FilePath = id + ".mp3",
            CoverPath = coverPath
        };

    // === GroupByAlbum ===

    [Fact]
    public void GroupByAlbum_Empty_Input_Returns_Empty_List()
    {
        var result = LibraryGroupingService.GroupByAlbum(System.Array.Empty<Track>());
        Assert.Empty(result);
    }

    [Fact]
    public void GroupByAlbum_Groups_By_AlbumArtist_When_Present_Else_By_Artist()
    {
        var tracks = new[]
        {
            T(1, "A", "Bowie",   "Hunky Dory", albumArtist: "David Bowie"),
            T(2, "B", "Bowie",   "Hunky Dory", albumArtist: "David Bowie"),
            T(3, "C", "Queen",   "The Works",  /* AlbumArtist=null */),
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Artist == "David Bowie" && a.Title == "Hunky Dory" && a.Tracks.Count == 2);
        Assert.Contains(result, a => a.Artist == "Queen" && a.Title == "The Works" && a.Tracks.Count == 1);
    }

    [Fact]
    public void GroupByAlbum_Compilations_Merge_Under_Various_Artists()
    {
        var tracks = new[]
        {
            T(1, "X", "Queen",      "Greatest Hits 1990", albumArtist: "Various Artists"),
            T(2, "Y", "Beatles",    "Greatest Hits 1990", albumArtist: "Various Artists"),
            T(3, "Z", "Pink Floyd", "Greatest Hits 1990", albumArtist: "Various Artists"),
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);

        var album = Assert.Single(result);
        Assert.Equal("Various Artists", album.Artist);
        Assert.Equal("Greatest Hits 1990", album.Title);
        Assert.Equal(3, album.Tracks.Count);
    }

    [Fact]
    public void GroupByAlbum_Sorts_Tracks_By_TrackNumber_NullsLast_Then_Title()
    {
        var tracks = new[]
        {
            T(1, "Beta",  "A", "X", trackNo: 2),
            T(2, "Alpha", "A", "X", trackNo: 1),
            T(3, "Zeta",  "A", "X", /* TrackNumber=null */),
            T(4, "Gamma", "A", "X", /* TrackNumber=null */),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();

        // 1 (Alpha, TN=1), 2 (Beta, TN=2), then nulls by title: Gamma, Zeta.
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma", "Zeta" }, album.Tracks.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void GroupByAlbum_Year_Equals_Max_Of_Track_Years_NullsIgnored()
    {
        var tracks = new[]
        {
            T(1, "A", "X", "Y", year: 2010),
            T(2, "B", "X", "Y", year: 2012),
            T(3, "C", "X", "Y", year: null),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();
        Assert.Equal(2012, album.Year);
    }

    [Fact]
    public void GroupByAlbum_CoverPath_Equals_First_Track_By_Id()
    {
        var tracks = new[]
        {
            T(7, "A", "X", "Y", coverPath: "cover7.jpg"),
            T(3, "B", "X", "Y", coverPath: "cover3.jpg"),
            T(5, "C", "X", "Y", coverPath: "cover5.jpg"),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();
        Assert.Equal("cover3.jpg", album.CoverPath);
    }

    [Fact]
    public void GroupByAlbum_Sorts_Albums_By_Year_Desc_NullsLast_Then_Title_Asc()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "Album Z", year: 2020),
            T(2, "b", "X", "Album A", year: 2020),  // same year, title asc → Album A first
            T(3, "c", "X", "Album M", year: 2022),  // newest
            T(4, "d", "X", "Album N", /* year=null */), // unknown year → last
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);
        Assert.Equal(new[] { "Album M", "Album A", "Album Z", "Album N" }, result.Select(a => a.Title).ToArray());
    }

    // === GroupByArtist ===

    [Fact]
    public void GroupByArtist_Groups_By_Track_Artist_Not_AlbumArtist()
    {
        // Compilation: трек Queen в альбоме «Various Artists» — должен попасть к Queen.
        var tracks = new[]
        {
            T(1, "A", "Queen",   "Comp 1990", albumArtist: "Various Artists"),
            T(2, "B", "Queen",   "Queen Hits"),
            T(3, "C", "Beatles", "Comp 1990", albumArtist: "Various Artists"),
        };

        var result = LibraryGroupingService.GroupByArtist(tracks);

        Assert.Equal(2, result.Count);
        var queen = Assert.Single(result, a => a.Name == "Queen");
        Assert.Equal(2, queen.TotalTracks);
        var beatles = Assert.Single(result, a => a.Name == "Beatles");
        Assert.Equal(1, beatles.TotalTracks);
    }

    [Fact]
    public void GroupByArtist_LooseTracks_Contains_Only_Empty_Album_Tracks()
    {
        var tracks = new[]
        {
            T(1, "Loose1",  "X", ""),
            T(2, "InAlb",   "X", "Some Album"),
            T(3, "Loose2",  "X", ""),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();

        Assert.Equal(2, artist.LooseTracks.Count);
        Assert.Equal(new[] { "Loose1", "Loose2" }, artist.LooseTracks.Select(t => t.Title).ToArray());
        Assert.Single(artist.Albums);
        Assert.Equal("Some Album", artist.Albums[0].Title);
    }

    [Fact]
    public void GroupByArtist_Sorts_Artists_By_TrackCount_Desc_Then_Name_Asc()
    {
        var tracks = new[]
        {
            T(1, "a", "Bob",   "X"),
            T(2, "b", "Alice", "X"),
            T(3, "c", "Alice", "Y"),
            T(4, "d", "Carl",  "X"),
            T(5, "e", "Carl",  "Y"),
        };

        var result = LibraryGroupingService.GroupByArtist(tracks);

        // Alice=2, Carl=2 — count tie → name asc (Alice, Carl). Bob=1 last.
        Assert.Equal(new[] { "Alice", "Carl", "Bob" }, result.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void GroupByArtist_TotalDuration_Sums_All_Tracks()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "A", durationSec: 100),
            T(2, "b", "X", "A", durationSec: 200),
            T(3, "c", "X", "",  durationSec: 50),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();
        Assert.Equal(System.TimeSpan.FromSeconds(350), artist.TotalDuration);
    }

    [Fact]
    public void GroupByArtist_Sorts_Albums_By_Year_Desc_NullsLast()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "Old",     year: 2010),
            T(2, "b", "X", "New",     year: 2024),
            T(3, "c", "X", "Unknown", /* year=null */),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();

        Assert.Equal(new[] { "New", "Old", "Unknown" }, artist.Albums.Select(a => a.Title).ToArray());
    }
}
```

- [ ] **3.4 — Запустить тесты — упадут с ошибкой компиляции (LibraryGroupingService не существует)**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~LibraryGroupingServiceTests" --nologo`

Expected: build error: `LibraryGroupingService` not found.

- [ ] **3.5 — Реализовать `LibraryGroupingService`**

Files: `MusicLibrary/Services/Library/LibraryGroupingService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Pure-функции группировки треков в альбомы и исполнителей.
/// Ключ альбома: (AlbumArtist ?? Artist, Album).
/// Ключ исполнителя: Track.Artist (НЕ AlbumArtist) — в compilations
/// каждый исполнитель виден сам по себе.
/// </summary>
public static class LibraryGroupingService
{
    public static IReadOnlyList<AlbumAggregate> GroupByAlbum(IReadOnlyList<Track> tracks)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return Array.Empty<AlbumAggregate>();
        }

        var groups = tracks
            .GroupBy(t => (
                Artist: string.IsNullOrWhiteSpace(t.AlbumArtist) ? t.Artist : t.AlbumArtist!,
                Title: t.Album ?? string.Empty));

        var aggregates = new List<AlbumAggregate>();
        foreach (var g in groups)
        {
            var sortedTracks = g
                .OrderBy(t => t.TrackNumber.HasValue ? 0 : 1)
                .ThenBy(t => t.TrackNumber ?? int.MaxValue)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var year = g
                .Where(t => t.Year.HasValue)
                .Select(t => (int?)t.Year!.Value)
                .DefaultIfEmpty(null)
                .Max();

            var firstByid = g.OrderBy(t => t.Id).First();

            aggregates.Add(new AlbumAggregate(
                Title: g.Key.Title,
                Artist: g.Key.Artist,
                Year: year,
                CoverPath: firstByid.CoverPath ?? string.Empty,
                Tracks: sortedTracks));
        }

        // Сортировка альбомов: Year DESC NULLS LAST, Title ASC.
        return aggregates
            .OrderBy(a => a.Year.HasValue ? 0 : 1)
            .ThenByDescending(a => a.Year ?? 0)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ArtistAggregate> GroupByArtist(IReadOnlyList<Track> tracks)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return Array.Empty<ArtistAggregate>();
        }

        var byArtist = tracks
            .GroupBy(t => t.Artist ?? string.Empty);

        var aggregates = new List<ArtistAggregate>();
        foreach (var ag in byArtist)
        {
            // Треки этого артиста — отдельно те, что в альбомах, и те, что «прочие» (Album пустой).
            var withAlbum = ag.Where(t => !string.IsNullOrWhiteSpace(t.Album)).ToList();
            var loose = ag
                .Where(t => string.IsNullOrWhiteSpace(t.Album))
                .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var albums = GroupByAlbum(withAlbum);

            var totalDuration = TimeSpan.Zero;
            foreach (var t in ag)
            {
                totalDuration += t.Duration;
            }

            aggregates.Add(new ArtistAggregate(
                Name: ag.Key,
                Albums: albums,
                LooseTracks: loose,
                TotalTracks: ag.Count(),
                TotalDuration: totalDuration));
        }

        // Сортировка исполнителей: TotalTracks DESC, Name ASC.
        return aggregates
            .OrderByDescending(a => a.TotalTracks)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
```

- [ ] **3.6 — Запустить тесты — все 12 должны пройти**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~LibraryGroupingServiceTests" --nologo`
Expected: 12 passed.

- [ ] **3.7 — Полный прогон**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 228 passed (216 + 12 новых).

- [ ] **3.8 — Коммит**

```bash
git add MusicLibrary/Services/Library MusicLibrary.Tests/Library
git commit -m "feat(grouping): LibraryGroupingService — pure GroupByAlbum/GroupByArtist

Two pure functions producing computed Album/Artist aggregates from a
flat track list. Used by the upcoming Albums/Artists views to render
the left column without introducing first-class entities (see ADR-0001).

GroupByAlbum keys on (AlbumArtist ?? Artist, Album) — compilations
with 'Various Artists' AlbumArtist coalesce into one album.
GroupByArtist keys on Track.Artist — each contributor stays visible
individually even when their track is on a compilation.

Twelve unit tests cover: empty input, compilations merging, track
sorting by (TrackNumber ASC NULLS LAST, Title ASC), album year =
max(non-null), cover = first track by Id, album sorting (Year DESC
NULLS LAST, Title ASC), artist sorting (TotalTracks DESC, Name ASC),
LooseTracks isolation, TotalDuration sum."
```

---

## Task 4 — `MainViewMode` enum + tabs state в `MainViewModel` + computed aggregates

Минимально-инвазивное добавление: новый enum, новое property с persistence в KeyValueStore, пересчёт двух новых коллекций в существующем `ApplyFilters`.

**Files:**
- Create: `MusicLibrary/Services/Library/MainViewMode.cs`
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Modify: `MusicLibrary.Tests/MainViewModelTests.cs` (для базовой проверки переключения)

### Steps

- [ ] **4.1 — Создать `MainViewMode`**

Files: `MusicLibrary/Services/Library/MainViewMode.cs`

```csharp
namespace MusicLibrary.Services.Library;

/// <summary>Активный режим левой колонки: плоский список треков, альбомы или исполнители.</summary>
public enum MainViewMode
{
    Tracks = 0,
    Albums = 1,
    Artists = 2
}
```

- [ ] **4.2 — Добавить поля и property в `MainViewModel`**

Files: `MusicLibrary/ViewModels/MainViewModel.cs`

В блок private fields (рядом с другими `_minRating`, `_reactionFilter` и т.п.):
```csharp
private MainViewMode _activeView = MainViewMode.Tracks;
private System.Collections.Generic.IReadOnlyList<AlbumAggregate> _displayedAlbums = System.Array.Empty<AlbumAggregate>();
private System.Collections.Generic.IReadOnlyList<ArtistAggregate> _displayedArtists = System.Array.Empty<ArtistAggregate>();
```

И publicиз `using MusicLibrary.Services.Library;` если ещё не подключён.

В блок publics:
```csharp
public MainViewMode ActiveView
{
    get => _activeView;
    set
    {
        if (SetProperty(ref _activeView, value))
        {
            _playerSettingsRepository?.SaveActiveView(value); // см. Step 4.4
        }
    }
}

public System.Collections.Generic.IReadOnlyList<AlbumAggregate> DisplayedAlbums
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

public System.Collections.Generic.IReadOnlyList<ArtistAggregate> DisplayedArtists
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
```

- [ ] **4.3 — В конце `ApplyFilters()` пересчитать агрегаты**

Files: `MusicLibrary/ViewModels/MainViewModel.cs`

Найти `private void ApplyFilters()`. В самом конце (после цикла, который наполняет `DisplayedTracks`):
```csharp
// Computed-агрегаты пересчитываются вместе с DisplayedTracks. На 50k треков ~50ms.
var filteredSnapshot = DisplayedTracks.ToList();
DisplayedAlbums = LibraryGroupingService.GroupByAlbum(filteredSnapshot);
DisplayedArtists = LibraryGroupingService.GroupByArtist(filteredSnapshot);
```

- [ ] **4.4 — Persistence ActiveView в KeyValueStore**

Files: `MusicLibrary/ViewModels/MainViewModel.cs` + `MusicBakh.Core/Abstractions/IPlayerSettingsRepository.cs` + `MusicBakh.Infrastructure/Persistence/Repositories/SqlitePlayerSettingsRepository.cs`

В `IPlayerSettingsRepository` добавить:
```csharp
int? LoadActiveViewIndex();
void SaveActiveView(MainViewMode view);
```

В `SqlitePlayerSettingsRepository` реализовать через тот же KV-механизм, что и существующие настройки (ключ `active_view`):
```csharp
public int? LoadActiveViewIndex()
{
    using var ctx = _contextFactory();
    var entry = ctx.KeyValueStore.FirstOrDefault(k => k.Key == "active_view");
    return entry is null ? null : int.Parse(entry.Value, System.Globalization.CultureInfo.InvariantCulture);
}

public void SaveActiveView(MusicLibrary.Services.Library.MainViewMode view)
{
    using var ctx = _contextFactory();
    var entry = ctx.KeyValueStore.FirstOrDefault(k => k.Key == "active_view");
    if (entry is null)
    {
        ctx.KeyValueStore.Add(new KeyValueEntryEntity { Key = "active_view", Value = ((int)view).ToString(System.Globalization.CultureInfo.InvariantCulture) });
    }
    else
    {
        entry.Value = ((int)view).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    ctx.SaveChanges();
}
```

⚠️ `MusicBakh.Core` сейчас НЕ ссылается на `MusicLibrary.Services.Library`. Использование `MainViewMode` из Infrastructure либо ломает зависимости либо требует enum в Core. Простое решение — **держать enum в Core**.

**Поправка к Step 4.1:** создавай `MainViewMode.cs` в `MusicBakh.Core/Domain/MainViewMode.cs`, а не в `MusicLibrary/Services/Library/`. (Это не «доменная сущность», но enum для совместного использования VM ↔ Repository — в Core.) Все импорты в этом плане соответственно.

В Presentation-DI бутстрапа (`App.xaml.cs` → `InitializeDatabase` или похожее) после Load `PlayerSettings` подгружаем `ActiveView`:
```csharp
int? savedView = _playerSettingsRepository.LoadActiveViewIndex();
if (savedView.HasValue && System.Enum.IsDefined(typeof(MainViewMode), savedView.Value))
{
    _viewModel.ActiveView = (MainViewMode)savedView.Value;
}
```

(Точное место зависит от того, как 1.0.3 уже гидратирует громкость/repeat — повтори тот же паттерн.)

- [ ] **4.5 — Тест на пересчёт агрегатов при смене фильтра**

Files: `MusicLibrary.Tests/MainViewModelTests.cs`

Перед закрывающей `}` класса:
```csharp
[Fact]
public void DisplayedAlbums_And_DisplayedArtists_Recompute_When_Filters_Change()
{
    var tracks = new[]
    {
        new Track { Id = 1, Title = "A", Artist = "X", Album = "Q", Genre = "Рок", FilePath = "1.mp3" },
        new Track { Id = 2, Title = "B", Artist = "X", Album = "Q", Genre = "Рок", FilePath = "2.mp3" },
        new Track { Id = 3, Title = "C", Artist = "Y", Album = "R", Genre = "Поп", FilePath = "3.mp3" },
    };
    var vm = CreateViewModel(tracks, /* остальные fakes */);

    // По умолчанию все три → два альбома, два исполнителя.
    Assert.Equal(2, vm.DisplayedAlbums.Count);
    Assert.Equal(2, vm.DisplayedArtists.Count);

    vm.SelectedGenre = "Рок";
    Assert.Single(vm.DisplayedAlbums);
    Assert.Equal("Q", vm.DisplayedAlbums[0].Title);
    Assert.Single(vm.DisplayedArtists);
    Assert.Equal("X", vm.DisplayedArtists[0].Name);
}
```

(Подбери реальный конструктор `CreateViewModel` из существующих fakes — он у вас уже есть.)

- [ ] **4.6 — Запустить тесты**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 229 passed (228 + 1 новый).

- [ ] **4.7 — Коммит**

```bash
git add MusicBakh.Core MusicBakh.Infrastructure MusicLibrary MusicLibrary.Tests
git commit -m "feat(viewmodel): MainViewMode enum + DisplayedAlbums/Artists computed in ApplyFilters

MainViewModel exposes ActiveView (Tracks/Albums/Artists) persisted to
KeyValueStore via IPlayerSettingsRepository. DisplayedAlbums and
DisplayedArtists are pure-function projections of the filtered track
snapshot (LibraryGroupingService); they recompute as part of every
ApplyFilters() pass so existing search/genre/tag/rating/reaction
filters automatically affect grouped views."
```

---

## Task 5 — Drill-down навигация: `LeftColumnState` + back-стек

**Files:**
- Create: `MusicLibrary/Services/Library/LeftColumnState.cs`
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Create: `MusicLibrary.Tests/ViewModels/MainViewModelNavigationTests.cs`

### Steps

- [ ] **5.1 — Создать `LeftColumnState`**

Files: `MusicLibrary/Services/Library/LeftColumnState.cs`

```csharp
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Discriminated union состояний левой колонки. Записи (record-наследники
/// abstract record) позволяют биндингам и TemplateSelector-у различать варианты.
/// </summary>
public abstract record LeftColumnState
{
    public sealed record TracksRoot : LeftColumnState;
    public sealed record AlbumsRoot : LeftColumnState;
    public sealed record ArtistsRoot : LeftColumnState;
    public sealed record AlbumDetail(AlbumAggregate Album) : LeftColumnState;
    public sealed record ArtistDetail(ArtistAggregate Artist) : LeftColumnState;
}
```

- [ ] **5.2 — Добавить navigation в `MainViewModel`**

Files: `MusicLibrary/ViewModels/MainViewModel.cs`

В private fields:
```csharp
private readonly System.Collections.Generic.Stack<LeftColumnState> _navStack = new();
private LeftColumnState _currentLeftColumn = new LeftColumnState.TracksRoot();
```

В publics:
```csharp
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

public bool CanGoBack => _navStack.Count > 0;

public System.Windows.Input.ICommand SwitchViewCommand { get; }
public System.Windows.Input.ICommand OpenAlbumCommand { get; }
public System.Windows.Input.ICommand OpenArtistCommand { get; }
public System.Windows.Input.ICommand BackCommand { get; }
```

В конструкторе (после остальных command-инициализаций):
```csharp
SwitchViewCommand = new RelayCommand(p =>
{
    if (p is not MainViewMode mode) return;
    ActiveView = mode;
    _navStack.Clear();
    CurrentLeftColumn = mode switch
    {
        MainViewMode.Tracks  => new LeftColumnState.TracksRoot(),
        MainViewMode.Albums  => new LeftColumnState.AlbumsRoot(),
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
```

⚠️ Когда `ActiveView` меняется, `CurrentLeftColumn` уже обновляется в `SwitchViewCommand`. Но если кто-то меняет `ActiveView` напрямую (`vm.ActiveView = ...`) — `CurrentLeftColumn` остаётся старым. Это допустимо, потому что в проде `ActiveView` меняется ТОЛЬКО через `SwitchViewCommand`. Тесты на это.

- [ ] **5.3 — Тесты**

Files: `MusicLibrary.Tests/ViewModels/MainViewModelNavigationTests.cs`

```csharp
using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using MusicLibrary.ViewModels;
using Xunit;

namespace MusicLibrary.Tests.ViewModels;

public sealed class MainViewModelNavigationTests
{
    private static MainViewModel CreateVM(params Track[] tracks)
    {
        // Используй те же fakes, что в Task 1 / 4 — fileService, audioPlayer и т.п.
        // Здесь должен быть фактический helper из тестового проекта.
        // (Эта строка — placeholder для имплементера; используй существующий CreateViewModelWith***.)
        throw new System.NotImplementedException("Реализатор: воспользуйся имеющимися test fakes.");
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
        var vm = CreateVM(/* несколько треков чтобы DisplayedAlbums был непустым */);
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
        var vm = CreateVM(/* несколько треков */);
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);

        vm.BackCommand.Execute(null);

        Assert.IsType<LeftColumnState.AlbumsRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void Artist_Then_Album_Then_Back_Returns_To_Artist_Detail()
    {
        var vm = CreateVM(/* треки чтобы DisplayedArtists был непустым и у одного из них был альбом */);
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
        var vm = CreateVM(/* треки */);
        vm.SwitchViewCommand.Execute(MainViewMode.Albums);
        vm.OpenAlbumCommand.Execute(vm.DisplayedAlbums[0]);
        Assert.True(vm.CanGoBack);

        vm.SwitchViewCommand.Execute(MainViewMode.Tracks);

        Assert.IsType<LeftColumnState.TracksRoot>(vm.CurrentLeftColumn);
        Assert.False(vm.CanGoBack);
    }
}
```

- [ ] **5.4 — Запустить тесты**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~MainViewModelNavigationTests" --nologo`
Expected: 6 passed.

- [ ] **5.5 — Полный прогон**

Expected: 235 passed (229 + 6 новых).

- [ ] **5.6 — Коммит**

```bash
git add MusicLibrary/Services/Library/LeftColumnState.cs MusicLibrary/ViewModels/MainViewModel.cs MusicLibrary.Tests/ViewModels/MainViewModelNavigationTests.cs
git commit -m "feat(viewmodel): drill-down navigation stack — LeftColumnState + commands

Discriminated union LeftColumnState models the five states of the left
column: three roots (Tracks/Albums/Artists) plus AlbumDetail and
ArtistDetail. SwitchViewCommand resets the back stack and rebases on
the new root. OpenAlbum/OpenArtist commands push the current state and
swap CurrentLeftColumn. BackCommand pops or no-ops.

CanGoBack signal lets the toolbar/back-button enable/disable cleanly.
Six unit tests cover default state, root switching, push, single back,
double back (Artist → Album → Artist), and stack reset on view switch."
```

---

## Task 6 — Play/Shuffle commands для альбома и исполнителя

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`

### Steps

- [ ] **6.1 — Добавить commands**

В private fields:
```csharp
private static readonly System.Random _shuffleRng = new();
```

(Если в проекте уже есть Random — переиспользуй.)

В publics:
```csharp
public System.Windows.Input.ICommand PlayAlbumCommand { get; }
public System.Windows.Input.ICommand ShuffleAlbumCommand { get; }
public System.Windows.Input.ICommand PlayArtistCommand { get; }
public System.Windows.Input.ICommand ShuffleArtistCommand { get; }
```

В конструкторе:
```csharp
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
```

И вспомогательный метод:
```csharp
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
```

⚠️ **Каверзный момент:** изменение `DisplayedTracks` через play-album обходит фильтры. Если пользователь стоит на «Treki + filter X», нажмёт «play album», `DisplayedTracks` заменится на треки альбома — фильтр визуально «съест» этот режим. **Это сознательное решение:** play-album / play-artist == «временная очередь», пользователь явно сменил намерение. Документировать в Sense-deviations.

⚠️ Альтернативно: ввести отдельную «play queue» отдельно от `DisplayedTracks`. Это правильнее, но **большой рефактор play-pipeline**. Откладываем — см. план разгрузки `MainViewModel` (роадмап 1.0.5-1.0.8).

- [ ] **6.2 — Smoke (без новых тестов — play-логика интегрирована с уже протестированной)**

Run: `dotnet build MusicLibrary/MusicLibrary.csproj --nologo`
Expected: 0 errors.

Run: `cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: запускается.

- [ ] **6.3 — Коммит**

```bash
git add MusicLibrary/ViewModels/MainViewModel.cs
git commit -m "feat(viewmodel): Play/Shuffle commands for album and artist aggregates

PlayAlbumCommand / ShuffleAlbumCommand replace DisplayedTracks with the
album's tracks (in order or Fisher-Yates shuffled) and start playback.
PlayArtistCommand / ShuffleArtistCommand do the same with the artist's
full discography (Albums.SelectMany + LooseTracks).

Trade-off: this overwrites DisplayedTracks, effectively temporarily
ignoring the active filter. Acceptable because the user explicitly
asked to play this set; documented in scope-deviations §1.0.4. Proper
separation of play-queue from DisplayedTracks belongs to the
MainViewModel decomposition roadmap (1.0.5+)."
```

---

## Task 7 — Tabs в шапке приложения

**Files:**
- Create: `MusicLibrary/Resources/MainViewTabsStyles.xaml`
- Modify: `MusicLibrary/App.xaml` (register MainViewTabsStyles)
- Modify: `MusicLibrary/MainWindow.xaml` (изменить шапку: добавить tabs)

### Steps

- [ ] **7.1 — Стиль для tabs**

Files: `MusicLibrary/Resources/MainViewTabsStyles.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Стиль кнопки-tab. Активный = подсвеченный золотом снизу, серый текст → золотой. -->
    <Style x:Key="MainViewTabButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0,0,0,2" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource MutedForegroundBrush}" />
        <Setter Property="FontFamily" Value="{StaticResource BodyFont}" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                          TextBlock.Foreground="{TemplateBinding Foreground}"
                                          TextBlock.FontWeight="{TemplateBinding FontWeight}" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
```

- [ ] **7.2 — Подключить в App.xaml**

В `<ResourceDictionary.MergedDictionaries>` после `TagChipStyles.xaml`:
```xml
<ResourceDictionary Source="Resources/MainViewTabsStyles.xaml" />
```

- [ ] **7.3 — Изменить шапку `MainWindow.xaml`**

Найти существующий блок `<Border Background="{StaticResource CardOverlayBrush}" ...> ... </Border>` (это шапка с логотипом + кнопками). Внутри его `<DockPanel>` структура была:
```xml
<Button DockPanel.Dock="Right" Content="＋ Добавить трек" ... />
<Button DockPanel.Dock="Right" Content="Статистика" ... />
<Button DockPanel.Dock="Right" Content="Теги" ... />
<StackPanel Orientation="Horizontal" ...>
    <Image .../>
    <StackPanel>... MusicBakh + Музыкальная библиотека ...</StackPanel>
</StackPanel>
```

Заменить на:
```xml
<DockPanel VerticalAlignment="Center">
    <Button DockPanel.Dock="Right" Content="＋ Добавить трек" Command="{Binding AddTrackCommand}" Style="{StaticResource SecondaryButtonStyle}" Height="42" Padding="18,0" />
    <Button DockPanel.Dock="Right" Content="Статистика" Command="{Binding OpenStatsCommand}" Style="{StaticResource SecondaryButtonStyle}" Height="42" Padding="18,0" Margin="0,0,12,0" ToolTip="Ctrl+T" />
    <Button DockPanel.Dock="Right" Content="Теги" Command="{Binding OpenTagsCommand}" Style="{StaticResource SecondaryButtonStyle}" Height="42" Padding="18,0" Margin="0,0,12,0" ToolTip="Ctrl+G" />

    <StackPanel DockPanel.Dock="Left" Orientation="Horizontal" VerticalAlignment="Center">
        <Image Width="54" Height="54" Source="Assets/Brand/musicbakh-logo.png"
               Stretch="Uniform" RenderOptions.BitmapScalingMode="HighQuality" />
        <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
            <TextBlock Text="MusicBakh" FontFamily="{StaticResource HeadingFont}"
                       Foreground="{StaticResource PrimaryBrush}" FontSize="28" FontWeight="SemiBold" />
            <TextBlock Text="Музыкальная библиотека" Foreground="{StaticResource MutedForegroundBrush}"
                       FontSize="13" Margin="1,2,0,0" />
        </StackPanel>
    </StackPanel>

    <!-- Tabs занимают оставшуюся середину. -->
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="40,0,40,0">
        <Button Style="{StaticResource MainViewTabButtonStyle}" Content="Треки"
                Command="{Binding SwitchViewCommand}">
            <Button.CommandParameter>
                <x:Static Member="domain:MainViewMode.Tracks" />
            </Button.CommandParameter>
            <Button.Style>
                <Style TargetType="Button" BasedOn="{StaticResource MainViewTabButtonStyle}">
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding ActiveView}">
                            <DataTrigger.Value>
                                <x:Static Member="domain:MainViewMode.Tracks" />
                            </DataTrigger.Value>
                            <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
                            <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Button.Style>
        </Button>
        <!-- Аналогично для Albums и Artists — повтори блок с CommandParameter + Style.Triggers -->
    </StackPanel>
</DockPanel>
```

И в шапке `<Window>` добавить (если ещё нет):
```xml
xmlns:domain="clr-namespace:MusicBakh.Core.Domain;assembly=MusicBakh.Core"
```

⚠️ Если `MainViewMode` остался в `MusicLibrary.Services.Library` (не в Core), `xmlns:domain` указывает на тот namespace. Поправь в Step 4.1 — мы решили класть в Core.

- [ ] **7.4 — Сборка + smoke**

Run: `dotnet build MusicLibrary/MusicLibrary.csproj --nologo`
Expected: 0 errors.

Run: `cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: запуск без exception; в окне видна шапка с тремя tabs.

- [ ] **7.5 — Коммит**

```bash
git add MusicLibrary/Resources/MainViewTabsStyles.xaml MusicLibrary/App.xaml MusicLibrary/MainWindow.xaml
git commit -m "feat(ui): tabs Tracks/Albums/Artists in the app header

Three text-button tabs slot into the header DockPanel: logo on the left,
tabs in the centre (HorizontalAlignment=Center, VerticalAlignment=Bottom),
existing 'Теги/Статистика/+ Добавить' buttons on the right. Active tab
is highlighted via DataTrigger on ActiveView — gold underline + gold
text. New MainViewTabsStyles.xaml resource dictionary holds the style."
```

---

## Task 8 — DataTemplates: Tracks/Albums/Artists/AlbumDetail/ArtistDetail + Selector

**Files:**
- Create: `MusicLibrary/Resources/AlbumsArtistsTemplates.xaml`
- Create: `MusicLibrary/Selectors/LeftColumnTemplateSelector.cs`
- Modify: `MusicLibrary/App.xaml`
- Modify: `MusicLibrary/MainWindow.xaml` (заменить старый `<ListBox>` в левой колонке на `<ContentControl ContentTemplateSelector=...>`)

### Steps

- [ ] **8.1 — Создать `LeftColumnTemplateSelector`**

Files: `MusicLibrary/Selectors/LeftColumnTemplateSelector.cs`

```csharp
using System.Windows;
using System.Windows.Controls;
using MusicLibrary.Services.Library;

namespace MusicLibrary.Selectors;

/// <summary>
/// Выбирает DataTemplate по типу LeftColumnState. Сами шаблоны передаются
/// через свойства; они задаются в XAML рядом с ContentControl.
/// </summary>
public sealed class LeftColumnTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TracksTemplate { get; set; }
    public DataTemplate? AlbumsTemplate { get; set; }
    public DataTemplate? ArtistsTemplate { get; set; }
    public DataTemplate? AlbumDetailTemplate { get; set; }
    public DataTemplate? ArtistDetailTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        LeftColumnState.TracksRoot   => TracksTemplate,
        LeftColumnState.AlbumsRoot   => AlbumsTemplate,
        LeftColumnState.ArtistsRoot  => ArtistsTemplate,
        LeftColumnState.AlbumDetail  => AlbumDetailTemplate,
        LeftColumnState.ArtistDetail => ArtistDetailTemplate,
        _ => null
    };
}
```

- [ ] **8.2 — `AlbumsArtistsTemplates.xaml` — все пять шаблонов**

Files: `MusicLibrary/Resources/AlbumsArtistsTemplates.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- ========== Tracks view: переиспользует существующий ListBox с TrackCardTemplate ========== -->
    <DataTemplate x:Key="TracksRootTemplate">
        <ListBox ItemsSource="{Binding DataContext.DisplayedTracks, RelativeSource={RelativeSource AncestorType=Window}}"
                 SelectedItem="{Binding DataContext.SelectedTrack, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}"
                 ItemTemplate="{StaticResource TrackCardTemplate}"
                 ItemContainerStyle="{StaticResource TrackListBoxItemStyle}"
                 Style="{StaticResource PanelListBoxStyle}" />
    </DataTemplate>

    <!-- ========== Albums view: 3xN сетка плиток ========== -->
    <DataTemplate x:Key="AlbumTileTemplate">
        <Border Background="#332A2A3F" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1"
                CornerRadius="10" Padding="10" Margin="6" Cursor="Hand">
            <Border.InputBindings>
                <MouseBinding MouseAction="LeftClick"
                              Command="{Binding DataContext.OpenAlbumCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                              CommandParameter="{Binding}" />
            </Border.InputBindings>
            <StackPanel>
                <Border Width="130" Height="130" CornerRadius="6">
                    <Border.Background>
                        <ImageBrush ImageSource="{Binding CoverPath}" Stretch="UniformToFill" />
                    </Border.Background>
                </Border>
                <TextBlock Text="{Binding Title}" Foreground="{StaticResource ForegroundBrush}"
                           FontWeight="SemiBold" FontSize="13" TextTrimming="CharacterEllipsis"
                           Margin="0,8,0,2" />
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding Artist}" Foreground="{StaticResource MutedForegroundBrush}"
                               FontSize="11" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text=" · " Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" />
                    <TextBlock Text="{Binding Year}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" />
                </StackPanel>
            </StackPanel>
        </Border>
    </DataTemplate>

    <DataTemplate x:Key="AlbumsRootTemplate">
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding DataContext.DisplayedAlbums, RelativeSource={RelativeSource AncestorType=Window}}"
                          ItemTemplate="{StaticResource AlbumTileTemplate}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <UniformGrid Columns="3" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>
        </ScrollViewer>
    </DataTemplate>

    <!-- ========== Artists view: вертикальный список ========== -->
    <DataTemplate x:Key="ArtistRowTemplate">
        <Border Background="#332A2A3F" BorderBrush="{StaticResource GoldBorderBrush}" BorderThickness="1"
                CornerRadius="10" Padding="10" Margin="0,0,0,6" Cursor="Hand">
            <Border.InputBindings>
                <MouseBinding MouseAction="LeftClick"
                              Command="{Binding DataContext.OpenArtistCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                              CommandParameter="{Binding}" />
            </Border.InputBindings>
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <!-- Avatar: круг с буквой инициала -->
                <Border Width="40" Height="40" CornerRadius="20" Background="{StaticResource PrimaryBrush}">
                    <TextBlock Text="{Binding Name, Converter={StaticResource FirstLetterConverter}}"
                               Foreground="{StaticResource BackgroundBrush}" FontWeight="SemiBold" FontSize="18"
                               HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <StackPanel Margin="12,0,0,0">
                    <TextBlock Text="{Binding Name}" Foreground="{StaticResource ForegroundBrush}"
                               FontWeight="SemiBold" FontSize="14" TextTrimming="CharacterEllipsis" />
                    <TextBlock Foreground="{StaticResource MutedForegroundBrush}" FontSize="11">
                        <Run Text="{Binding Albums.Count, Mode=OneWay}" />
                        <Run Text=" альбомов · " />
                        <Run Text="{Binding TotalTracks, Mode=OneWay}" />
                        <Run Text=" треков" />
                    </TextBlock>
                </StackPanel>
            </StackPanel>
        </Border>
    </DataTemplate>

    <DataTemplate x:Key="ArtistsRootTemplate">
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding DataContext.DisplayedArtists, RelativeSource={RelativeSource AncestorType=Window}}"
                          ItemTemplate="{StaticResource ArtistRowTemplate}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <StackPanel />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>
        </ScrollViewer>
    </DataTemplate>

    <!-- ========== AlbumDetail (внутри drill-down) ========== -->
    <DataTemplate x:Key="AlbumDetailTemplate">
        <DockPanel LastChildFill="True">
            <!-- Back -->
            <Button DockPanel.Dock="Top" Content="← Все альбомы"
                    Command="{Binding DataContext.BackCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                    Background="Transparent" BorderThickness="0" Foreground="{StaticResource PrimaryBrush}"
                    HorizontalAlignment="Left" Padding="8,4" Margin="0,0,0,8" Cursor="Hand" />

            <!-- Header (Album.Cover + Title + Artist + meta) -->
            <Border DockPanel.Dock="Top" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal">
                    <Border Width="100" Height="100" CornerRadius="8" BorderBrush="{StaticResource PrimaryBrush}" BorderThickness="1">
                        <Border.Background>
                            <ImageBrush ImageSource="{Binding Album.CoverPath}" Stretch="UniformToFill" />
                        </Border.Background>
                    </Border>
                    <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
                        <TextBlock Text="{Binding Album.Title}" Foreground="{StaticResource ForegroundBrush}"
                                   FontWeight="SemiBold" FontSize="18" />
                        <TextBlock Text="{Binding Album.Artist}" Foreground="{StaticResource MutedForegroundBrush}"
                                   FontSize="13" Margin="0,2,0,4" />
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Album.Year, TargetNullValue='—'}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" />
                            <TextBlock Text=" · " Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" />
                            <TextBlock Foreground="{StaticResource MutedForegroundBrush}" FontSize="11">
                                <Run Text="{Binding Album.Tracks.Count, Mode=OneWay}" />
                                <Run Text=" треков" />
                            </TextBlock>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                            <Button Content="▶ Воспроизвести альбом" Style="{StaticResource PrimaryButtonStyle}" Height="32"
                                    Command="{Binding DataContext.PlayAlbumCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                    CommandParameter="{Binding Album}" />
                            <Button Content="🔀 Перемешать" Style="{StaticResource SecondaryButtonStyle}" Height="32" Margin="8,0,0,0"
                                    Command="{Binding DataContext.ShuffleAlbumCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                    CommandParameter="{Binding Album}" />
                        </StackPanel>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- Tracks list -->
            <ListBox ItemsSource="{Binding Album.Tracks}"
                     SelectedItem="{Binding DataContext.SelectedTrack, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}"
                     Style="{StaticResource PanelListBoxStyle}">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="6,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="30" />
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="{Binding TrackNumber, TargetNullValue=''}"
                                       Foreground="{StaticResource MutedForegroundBrush}" FontSize="12" />
                            <TextBlock Grid.Column="1" Text="{Binding Title}" Foreground="{StaticResource ForegroundBrush}"
                                       FontSize="13" TextTrimming="CharacterEllipsis" />
                            <TextBlock Grid.Column="2" Text="{Binding DurationText}"
                                       Foreground="{StaticResource MutedForegroundBrush}" FontSize="11"
                                       Margin="8,0,0,0" />
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>
    </DataTemplate>

    <!-- ========== ArtistDetail (внутри drill-down) ========== -->
    <DataTemplate x:Key="ArtistDetailTemplate">
        <DockPanel LastChildFill="True">
            <Button DockPanel.Dock="Top" Content="← Все исполнители"
                    Command="{Binding DataContext.BackCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                    Background="Transparent" BorderThickness="0" Foreground="{StaticResource PrimaryBrush}"
                    HorizontalAlignment="Left" Padding="8,4" Margin="0,0,0,8" Cursor="Hand" />

            <Border DockPanel.Dock="Top" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal">
                    <Border Width="70" Height="70" CornerRadius="35" Background="{StaticResource PrimaryBrush}">
                        <TextBlock Text="{Binding Artist.Name, Converter={StaticResource FirstLetterConverter}}"
                                   Foreground="{StaticResource BackgroundBrush}" FontWeight="SemiBold" FontSize="28"
                                   HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
                        <TextBlock Text="{Binding Artist.Name}" Foreground="{StaticResource ForegroundBrush}"
                                   FontWeight="SemiBold" FontSize="18" />
                        <TextBlock Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" Margin="0,2,0,0">
                            <Run Text="{Binding Artist.Albums.Count, Mode=OneWay}" />
                            <Run Text=" альбомов · " />
                            <Run Text="{Binding Artist.TotalTracks, Mode=OneWay}" />
                            <Run Text=" треков" />
                        </TextBlock>
                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                            <Button Content="▶ Слушать всё" Style="{StaticResource PrimaryButtonStyle}" Height="32"
                                    Command="{Binding DataContext.PlayArtistCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                    CommandParameter="{Binding Artist}" />
                            <Button Content="🔀 Перемешать" Style="{StaticResource SecondaryButtonStyle}" Height="32" Margin="8,0,0,0"
                                    Command="{Binding DataContext.ShuffleArtistCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                    CommandParameter="{Binding Artist}" />
                        </StackPanel>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- Scrollable content (Albums + LooseTracks) -->
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel>
                    <TextBlock Text="Альбомы" Foreground="{StaticResource PrimaryBrush}" FontWeight="SemiBold" FontSize="13" Margin="0,0,0,8" />
                    <ItemsControl ItemsSource="{Binding Artist.Albums}" ItemTemplate="{StaticResource AlbumTileTemplate}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <UniformGrid Columns="3" />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                    </ItemsControl>

                    <TextBlock Text="Прочие треки" Foreground="{StaticResource PrimaryBrush}" FontWeight="SemiBold" FontSize="13" Margin="0,12,0,8"
                               Visibility="{Binding Artist.LooseTracks.Count, Converter={StaticResource CountToVisibilityConverter}}" />
                    <ItemsControl ItemsSource="{Binding Artist.LooseTracks}"
                                  Visibility="{Binding Artist.LooseTracks.Count, Converter={StaticResource CountToVisibilityConverter}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin="6,4">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Text="{Binding Title}" Foreground="{StaticResource ForegroundBrush}" FontSize="13" TextTrimming="CharacterEllipsis" />
                                    <TextBlock Grid.Column="1" Text="{Binding DurationText}" Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" Margin="8,0,0,0" />
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>
            </ScrollViewer>
        </DockPanel>
    </DataTemplate>

</ResourceDictionary>
```

⚠️ В шаблонах используются два конвертера: `FirstLetterConverter` (берёт первую букву строки) и `CountToVisibilityConverter` (int → Visibility). Если их нет — создать.

- [ ] **8.3 — Создать FirstLetterConverter и CountToVisibilityConverter (если их нет)**

Files: `MusicLibrary/Converters/FirstLetterConverter.cs`

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.Length > 0) return s[0].ToString().ToUpperInvariant();
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

Files: `MusicLibrary/Converters/CountToVisibilityConverter.cs`

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>int → Visibility (0 = Collapsed, иначе Visible).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

Зарегистрировать в App.xaml (рядом с другими конвертерами):
```xml
<converters:FirstLetterConverter x:Key="FirstLetterConverter" />
<converters:CountToVisibilityConverter x:Key="CountToVisibilityConverter" />
```

- [ ] **8.4 — Подключить AlbumsArtistsTemplates.xaml в App.xaml**

```xml
<ResourceDictionary Source="Resources/AlbumsArtistsTemplates.xaml" />
```

(После `TrackTemplates.xaml`.)

- [ ] **8.5 — Заменить ListBox в MainWindow.xaml на ContentControl с TemplateSelector**

В левой колонке `<Border Grid.Column="0">` (внутри `<Grid Grid.Row="2">`) сейчас:
```xml
<DockPanel Margin="24">
    <ListBox ItemsSource="{Binding DisplayedTracks}" ... />
</DockPanel>
```

Заменить на:
```xml
<DockPanel Margin="24">
    <ContentControl Content="{Binding CurrentLeftColumn}">
        <ContentControl.ContentTemplateSelector>
            <selectors:LeftColumnTemplateSelector
                TracksTemplate="{StaticResource TracksRootTemplate}"
                AlbumsTemplate="{StaticResource AlbumsRootTemplate}"
                ArtistsTemplate="{StaticResource ArtistsRootTemplate}"
                AlbumDetailTemplate="{StaticResource AlbumDetailTemplate}"
                ArtistDetailTemplate="{StaticResource ArtistDetailTemplate}" />
        </ContentControl.ContentTemplateSelector>
    </ContentControl>
</DockPanel>
```

И в шапке `<Window>` (если ещё нет):
```xml
xmlns:selectors="clr-namespace:MusicLibrary.Selectors"
```

- [ ] **8.6 — Сборка + smoke**

Run:
```bash
dotnet build MusicLibrary/MusicLibrary.csproj --nologo
cd MusicLibrary && timeout 8 dotnet run --no-build --verbosity quiet 2>&1 | tail -3
```
Expected: 0 errors, приложение работает. Tabs кликабельны, левая колонка меняет content. Клик по альбому/исполнителю открывает detail. «← Все альбомы» возвращает.

- [ ] **8.7 — Полный прогон тестов**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 235 passed.

- [ ] **8.8 — Коммит**

```bash
git add MusicLibrary/Resources/AlbumsArtistsTemplates.xaml MusicLibrary/Selectors MusicLibrary/Converters MusicLibrary/App.xaml MusicLibrary/MainWindow.xaml
git commit -m "feat(ui): five DataTemplates + LeftColumnTemplateSelector for albums/artists drill-down

Left column becomes a ContentControl bound to CurrentLeftColumn (state
machine introduced in Task 5). LeftColumnTemplateSelector picks one of
five templates by state type: TracksRoot (existing ListBox),
AlbumsRoot (UniformGrid Columns=3 of AlbumTile), ArtistsRoot (vertical
StackPanel of ArtistRow), AlbumDetail (back + cover + meta + tracks +
Play/Shuffle), ArtistDetail (back + avatar + meta + albums sub-grid +
loose tracks).

Two new converters land too: FirstLetterConverter (artist avatar
initial) and CountToVisibilityConverter (hide LooseTracks section when
empty)."
```

---

## Task 9 — Хоткеи Ctrl+1/2/3 и Esc-back

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml` (Window.InputBindings)
- Modify: `MusicLibrary/MainWindow.xaml.cs` (Esc-back logic с приоритетом search)

### Steps

- [ ] **9.1 — Добавить InputBindings**

Files: `MusicLibrary/MainWindow.xaml`

В `<Window.InputBindings>` (там уже есть Ctrl+F, Ctrl+T, Ctrl+G и т.п.) добавить:
```xml
<KeyBinding Key="D1" Modifiers="Control" Command="{Binding SwitchViewCommand}">
    <KeyBinding.CommandParameter>
        <x:Static Member="domain:MainViewMode.Tracks" />
    </KeyBinding.CommandParameter>
</KeyBinding>
<KeyBinding Key="D2" Modifiers="Control" Command="{Binding SwitchViewCommand}">
    <KeyBinding.CommandParameter>
        <x:Static Member="domain:MainViewMode.Albums" />
    </KeyBinding.CommandParameter>
</KeyBinding>
<KeyBinding Key="D3" Modifiers="Control" Command="{Binding SwitchViewCommand}">
    <KeyBinding.CommandParameter>
        <x:Static Member="domain:MainViewMode.Artists" />
    </KeyBinding.CommandParameter>
</KeyBinding>
```

(`Key="D1"` в WPF — это цифра «1» на основной клавиатуре; Numpad — `Key="NumPad1"`. Если нужны оба — два KeyBinding.)

- [ ] **9.2 — Esc приоритет: сначала очистка поиска, потом back**

Файл `MainWindow.xaml.cs`. Найди существующий KeyDown-handler `SearchBox` (с 1.0.2 он очищает Search на Esc). Логика уже работает: если фокус в SearchBox, Esc обрабатывается там и `e.Handled = true`. Если фокус НЕ в SearchBox — событие пропускается до Window. Значит, надо добавить **window-level KeyDown** на Esc:

```csharp
private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (e.Key == System.Windows.Input.Key.Escape && _viewModel.CanGoBack && !SearchBox.IsKeyboardFocusWithin)
    {
        _viewModel.BackCommand.Execute(null);
        e.Handled = true;
    }
}
```

В XAML `<Window>` добавить:
```xml
KeyDown="Window_KeyDown"
```

- [ ] **9.3 — Smoke**

Run: `cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: запускается, Ctrl+1/2/3 переключают tabs, Esc внутри drill-down делает back (если поиск не в фокусе).

- [ ] **9.4 — Коммит**

```bash
git add MusicLibrary/MainWindow.xaml MusicLibrary/MainWindow.xaml.cs
git commit -m "feat(ui): hotkeys Ctrl+1/2/3 for tabs + Esc-back with search priority

Ctrl+1/2/3 → SwitchViewCommand(Tracks/Albums/Artists). Window-level
KeyDown listens for Esc and dispatches BackCommand, but only when
CanGoBack is true AND the SearchBox is NOT focused — that way the
existing 'Esc clears search' behavior keeps priority."
```

---

## Task 10 — Manual smoke + docs (changelog 1.0.4, scope-deviations, architecture, README, release-checklist)

- [ ] **10.1 — Запустить приложение и пройти чек-лист**

Run: `dotnet run --project MusicLibrary`

Проверки:
- [ ] Tabs «Треки/Альбомы/Исполнители» в шапке. Активный подсвечен.
- [ ] Ctrl+1/2/3 переключают tabs.
- [ ] При переключении tab левая колонка меняется. Middle/right колонки не двигаются.
- [ ] Active tab сохраняется при перезапуске.
- [ ] В Albums view — сетка 3×N плиток с обложками. Empty state на пустой библиотеке.
- [ ] В Artists view — вертикальный список с круглыми аватарками-инициалами.
- [ ] Клик по альбому → drill-down. «← Все альбомы» возвращает.
- [ ] Клик по исполнителю → drill-down. Видны albums sub-grid + Loose tracks (если есть).
- [ ] Клик по альбому внутри Artist detail → drill ещё глубже. Esc возвращает на Artist detail.
- [ ] Esc на Artist detail → возвращает на Artists root.
- [ ] Кнопки «▶ Воспроизвести альбом» и «🔀 Перемешать» работают: заменяют DisplayedTracks на треки альбома, стартует первый.
- [ ] Кнопки «▶ Слушать всё» и «🔀 Перемешать» в Artist detail — аналогично для всей дискографии.
- [ ] Фильтры (search/genre/tag/rating/reaction) работают и в Tracks, и в Albums/Artists (фильтруют треки, потом группируются).
- [ ] Импорт нового mp3 заполняет Year/TrackNumber/AlbumArtist (если они в тегах).
- [ ] Compilations с `AlbumArtist="Various Artists"` группируются в один альбом; в Artists view каждый исполнитель отдельно.
- [ ] Бенчмарк FTS не просел: `dotnet test --filter "Category=Benchmark"`.

Если что-то не работает — фикс инлайн, коммит.

- [ ] **10.2 — Apgrade-smoke с 1.0.3 на 1.0.4**

Если есть БД от 1.0.3 — скопируй её, запусти 1.0.4. Миграция `AddTrackYearNumberAlbumArtist` должна накатиться, существующие треки получат NULL в новых полях, библиотека на месте.

- [ ] **10.3 — Создать `docs/changelog/1.0.4.md`** (по образцу 1.0.3.md):

Сюда содержание брать из spec'а: tabs, drill-down, грид/список, новые поля Track, computed-агрегаты, compilations, хоткеи Ctrl+1/2/3 + Esc-back, ADR-0001.

- [ ] **10.4 — Обновить `docs/architecture.md`**

Bump версии 1.0.3 → 1.0.4. Добавить:
- В каталог проектов: `LibraryGroupingService.cs`, `AlbumAggregate.cs`, `ArtistAggregate.cs`, `LeftColumnState.cs`, `MainViewMode.cs`, `LeftColumnTemplateSelector.cs`, `AlbumsArtistsTemplates.xaml`, `MainViewTabsStyles.xaml`, новые конвертеры.
- В таблицу миграций: `AddTrackYearNumberAlbumArtist`.
- Новая секция «Альбомы и исполнители» — describes computed-aggregates подход + ссылка на ADR-0001.
- Обновить тест-счётчик: 213 → 235.

- [ ] **10.5 — Обновить `docs/scope-deviations.md`**

Добавить §1.0.4 с пунктами:
- Computed-агрегаты вместо first-class (см. ADR-0001).
- Compilations работают только с тегом AlbumArtist.
- Биография артиста и обложка артиста отложены до 1.3.0.
- Play-album/Play-artist переписывают DisplayedTracks — временно игнорируя активный фильтр. Полная разгрузка — 1.0.5+.
- Без виртуализации в Albums/Artists views (хватает встроенной).
- Без context menus, drag-and-drop.

- [ ] **10.6 — Обновить `README.md`**

Добавить в features list: три views (Tracks/Albums/Artists), drill-down, compilations через AlbumArtist, Ctrl+1/2/3 хоткеи.

- [ ] **10.7 — Обновить `docs/release-checklist.md`**

В секцию smoke добавить:
- Tabs переключение через клик и Ctrl+1/2/3.
- Albums view: сетка с обложками; клик открывает detail; кнопки Play/Shuffle работают.
- Artists view: список; клик открывает detail; albums sub-grid; loose tracks (если есть); drill в album изнутри Artist; back возвращает на Artist detail.
- Compilations: если в тегах есть AlbumArtist="Various Artists", все треки слились в один альбом.
- Apgrade-smoke: миграция накатилась без потери данных.

- [ ] **10.8 — Коммит docs**

```bash
git add docs/changelog/1.0.4.md docs/architecture.md docs/scope-deviations.md docs/release-checklist.md README.md
git commit -m "docs: iteration D / v1.0.4 release notes

Changelog, architecture bump, scope-deviations §1.0.4, README features
list, release-checklist smoke items — all updated for tabs + drill-down
+ computed Album/Artist aggregates."
```

---

## Task 11 — Release 1.0.4

- [ ] **11.1 — Bump версии**

Files: `MusicLibrary/MusicLibrary.csproj`

```xml
<Version>1.0.4</Version>
<AssemblyVersion>1.0.4.0</AssemblyVersion>
<FileVersion>1.0.4.0</FileVersion>
```

- [ ] **11.2 — Release build + полный тест + бенчмарк**

```bash
dotnet build -c Release MusicLibrary/MusicLibrary.csproj --nologo
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category=Benchmark" --nologo
```
Expected: 0 warnings, 235 passed, бенчмарк ~7 мс (не должен просесть — мы не трогали FTS).

- [ ] **11.3 — Bump-коммит**

```bash
git add MusicLibrary/MusicLibrary.csproj
git commit -m "chore: bump version to 1.0.4 — iteration D release"
```

- [ ] **11.4 — Собрать инсталлятор**

```bash
pwsh scripts/build-release.ps1 -Version 1.0.4
```
Expected: `release/MusicBakh-Setup-1.0.4.exe` ~80 МБ.

- [ ] **11.5 — Тег + push ветки**

```bash
git tag -a v1.0.4 -m "Release 1.0.4 — Library 2.0 iteration D (Albums and Artists views)"
git push origin release/1.0.4-iteration-d
```

- [ ] **11.6 — Открыть PR в main**

`gh pr create` с template аналогично 1.0.3. Самая мощная итерация фазы A.

- [ ] **11.7 — После merge — push тег + GitHub Release**

```bash
git checkout main && git pull
git push origin v1.0.4
gh release create v1.0.4 --repo Linkimin/MusicBakh --title "v1.0.4 — Альбомы и Исполнители (итерация D)" --notes "..." release/MusicBakh-Setup-1.0.4.exe
```

---

## Definition of Done

- [ ] Все 11 задач отмечены.
- [ ] 235 unit-тестов зелёные (213 базовых + 22 новых: 2 на Track round-trip, 1 на metadata propagation, 12 на LibraryGroupingService, 1 на DisplayedAlbums/Artists, 6 на navigation).
- [ ] Бенчмарк FTS не просел (~7 мс на 50k треков).
- [ ] Все три tabs работают, drill-down пройден end-to-end.
- [ ] Compilations с AlbumArtist группируются правильно.
- [ ] Active tab сохраняется между сессиями.
- [ ] Apgrade с 1.0.3 проходит без потери данных.
- [ ] Инсталлятор `MusicBakh-Setup-1.0.4.exe` собран.

---

## Self-Review

**Spec coverage:**

- ✅ Tabs в шапке: Task 7.
- ✅ Три views (Tracks/Albums/Artists): Task 8 (DataTemplates).
- ✅ Drill-down state machine: Tasks 5 + 8.
- ✅ Filter semantics (фильтр → группировка): Task 4 (`ApplyFilters` пересчитывает аггрегаты).
- ✅ Track.Year/TrackNumber/AlbumArtist: Tasks 1 + 2.
- ✅ `LibraryGroupingService` + аггрегаты: Task 3.
- ✅ Compilations через AlbumArtist: тест в Task 3 + код в LibraryGroupingService.
- ✅ Сортировки (Year DESC NULLS LAST, TotalTracks DESC и т.п.): Task 3 (тесты + реализация).
- ✅ Active tab persistence: Task 4 (KeyValueStore через IPlayerSettingsRepository).
- ✅ Хоткеи Ctrl+1/2/3 + Esc-back: Task 9.
- ✅ Esc приоритет (search → back): Task 9.
- ✅ Play/Shuffle commands: Task 6.
- ✅ Cover альбома = первый трек по Id: Task 3.
- ✅ Avatar исполнителя = круг с инициалом: Task 8 (ArtistRowTemplate + FirstLetterConverter).
- ✅ Сознательные ограничения (без виртуализации, без context menus, без drag-and-drop, дубли альбомов): Task 10.5.
- ✅ ADR-0001 ссылается в Task 10.4 (architecture.md).

**Placeholder scan:** одно место — Step 5.3 `CreateVM` имеет `throw new NotImplementedException(...)`. Это плейсхолдер для имплементера — он должен переиспользовать существующие test fakes из MainViewModelTests.cs. Это **намеренно** оставлено, потому что универсальный helper в проекте не выделен, и копировать всю простыню в инструкции бессмысленно — имплементер скопирует из соседнего файла. Помечено комментарием.

**Type consistency:**
- `MainViewMode` → Step 4.1 (поправлено: в Core/Domain, не в Services.Library).
- `LeftColumnState` → Step 5.1 (в Services.Library, потому что использует AlbumAggregate из той же папки).
- `AlbumAggregate.AlbumKey` использует U+0000 (` `) как separator — в спеке тоже так.
- Commands: `SwitchViewCommand`, `OpenAlbumCommand`, `OpenArtistCommand`, `BackCommand`, `PlayAlbumCommand`, `ShuffleAlbumCommand`, `PlayArtistCommand`, `ShuffleArtistCommand` — все упомянуты в Tasks 5/6 и в XAML Task 8.
- `CanGoBack` — упомянут в Task 5 (VM) и используется в Task 9 (KeyDown handler).
- `IPlayerSettingsRepository.LoadActiveViewIndex/SaveActiveView` — Step 4.4 + использование в DI-бутстрапе.

План внутренне непротиворечив. Готов к исполнению.
