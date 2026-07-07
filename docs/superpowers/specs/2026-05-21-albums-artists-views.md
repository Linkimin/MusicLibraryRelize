# Спек: Альбомные и исполнительские views

**Дата:** 2026-05-21
**Статус:** утверждён в брейншторминге, ждёт плана реализации
**Версия:** `v1.0.4` (итерация D эпика «Library 2.0»)

## Контекст

После итераций A–C приложение умеет хранить треки в SQLite (`1.0.1`), искать через FTS5 и держать историю (`1.0.2`), а также поддерживает рейтинги/реакции/теги-ярлыки и горизонтальный фильтр-toolbar (`1.0.3`). Левая колонка показывает **плоский** список треков с фильтрацией через `LibraryFilter` pure-функцию.

Roadmap [`docs/roadmap-vision.md`](../../roadmap-vision.md) §4.1 для `1.1.0` требует «Альбомный и исполнительский view (группировка)». Это вторая по объёму фича итерации после ratings/tags — пользователь должен видеть свою библиотеку, сгруппированную по альбомам и исполнителям, и иметь возможность drill-down в детальную страницу.

Решение использовать computed-агрегаты (не first-class сущности) зафиксировано в [ADR-0001](../../adr/0001-album-artist-computed-aggregates.md). Этот спек **опирается на это решение**, не переопределяет его.

## Цель

1. Добавить **два новых view-режима**: «Альбомы» и «Исполнители» — равноправно с существующими «Треки».
2. Дать **drill-down**: клик по альбому/исполнителю открывает детальную страницу внутри левой колонки (без потери now-playing и истории справа).
3. Сохранить **единый фильтр-pipeline**: те же search/genre/tags/rating/reaction применяются к трекам, потом результаты группируются в альбомы/исполнителей.
4. Добавить три новых поля в `Track` (`Year`, `TrackNumber`, `AlbumArtist`) для правильной сортировки треклиста и группировки compilations.
5. Сохранить **производительность**: на библиотеке 50k треков переключение tabs и drill-down должны ощущаться мгновенно (< 200 мс).

## Структура экрана

```
┌──────────────────────────────────────────────────────────────────────┐
│ 🎵 MusicBakh │ [Треки] [Альбомы] [Исполнители] │ Теги · Стат. · +    │ ← шапка с tabs
├──────────────────────────────────────────────────────────────────────┤
│ 🔍 Поиск......  Жанр▾  утро • работа • +N ▾                    ⋮   │ ← toolbar 1.0.3 без изменений
├────────────────────────┬───────────────┬─────────────────────────────┤
│  ↓ контент по tab-у   │  Now Playing  │  History                    │
└────────────────────────┴───────────────┴─────────────────────────────┘
```

Шапка приложения (`Grid.Row=0`) расширяется до структуры:
- слева: логотип «🎵 MusicBakh» + подпись «Музыкальная библиотека».
- центр: три **tabs** «Треки» / «Альбомы» / «Исполнители», золотая подсветка активной (стиль уже есть в `StatsTabControlStyle`, переиспользуем или заведём `MainViewTabsStyle`).
- справа: существующие кнопки «Теги», «Статистика», «+ Добавить трек».

Активный tab сохраняется в `KeyValueStore` через `IPlayerSettingsRepository` (или аналогичный новый ключ). Пользователь возвращается туда, где был, между сессиями.

## Контент левой колонки по tab-у

### 1. «Треки» (без изменений)

То же, что в `1.0.3`: `ListBox` с `TrackCardTemplate`. Источник — `DisplayedTracks` (результат `LibraryFilter`).

### 2. «Альбомы»

**Layout:** сетка 3 в ряд, плитки квадратные ~150 px, scroll вертикальный. Спройти можно через `ItemsControl` с `UniformGrid Columns=3` в `ItemsPanel`.

**Плитка альбома:**
- Обложка квадратная (`CoverPath` первого трека в группе по `Track.Id ASC`).
- Под обложкой — название альбома (жирно, 1–2 строки с `TextTrimming=CharacterEllipsis`).
- Ниже — «Исполнитель · Год» приглушённо-серым (FontSize 11).
- Tooltip на hover: полное название альбома + N треков + общая длительность.
- Cursor=Hand, левый клик = drill-down.

**Сортировка плиток:** `Year DESC NULLS LAST, Title ASC`. Альбомы без `Year` уходят в конец.

**Empty state:** «Альбомов пока нет — добавьте треки с заполненным тегом Album» (центрированно, серым).

### 3. «Исполнители»

**Layout:** вертикальный список (одна колонка), компактные строки 56 px. Используется `ItemsControl` со `StackPanel` в `ItemsPanel`.

**Строка исполнителя:**
- Круглый avatar 40×40 с буквой инициала исполнителя (градиент по hash имени — детерминированно). Используем простой алгоритм: hash → один из 7 фирменных цветов палитры (как в `TagsViewModel.PresetColors`).
- Имя исполнителя (FontSize 14, SemiBold).
- Ниже — «N альбомов · M треков» серым, FontSize 11.
- Cursor=Hand, левый клик = drill-down.

**Сортировка:** `Tracks.Count DESC, Name ASC`. Самые «обильные» сверху.

**Empty state:** «Исполнителей пока нет — добавьте треки с заполненным тегом Artist».

## Drill-down: детальные страницы

Drill-down реализуется не через отдельные окна, а через **state machine внутри той же левой колонки**. ViewModel держит `LeftColumnState` enum или discriminated union с состояниями:
- `Tracks`, `Albums`, `Artists` — корневые tabs.
- `AlbumDetail(AlbumKey)` — drill-down из Albums.
- `ArtistDetail(ArtistName)` — drill-down из Artists.
- `AlbumDetailViaArtist(ArtistName, AlbumKey)` — drill-down из Artist → Album. Back возвращает на ArtistDetail.

Back-стек хранится в простом `Stack<LeftColumnState>`. При переключении tab-а стек обнуляется до этого tab-а как корня (drill-down состояние НЕ сохраняется между tabs).

### Album detail

**Layout** (внутри левой колонки):
```
← Все альбомы
┌────────┐ Heroes
│ cover  │ Sabaton
│ 100×100│ 2014 · 11 треков · 45 мин
└────────┘ [▶ Воспроизвести альбом]  [🔀 Перемешать]

1.  ▶ Night Witches             4:03
2.    No Bullets Fly             4:31
3.    Smoking Snakes             4:13
...
```

- «← Все альбомы» — кликабельный label, возвращает в Albums grid.
- Обложка 100×100, рамка золотая, скруглённая.
- Метаданные справа: название (жирно, 18px), исполнитель (серый), «год · N треков · длительность» (мелко-серым).
- Две кнопки: «▶ Воспроизвести альбом» (заменяет очередь воспроизведения на треки этого альбома и стартует первый) и «🔀 Перемешать» (shuffle тех же треков).
- Треклист: компактные строки без обложек. `«№. ▶ Название    Длительность»`. Текущий играющий выделен золотой подсветкой (тот же `TrackIdentityMatchConverter`). Двойной клик на строке = воспроизведение этого трека.
- Сортировка треков: `TrackNumber ASC NULLS LAST, Title ASC`.

**Esc** = back.

### Artist detail

**Layout** (внутри левой колонки):
```
← Все исполнители
   ⬤   Sabaton
  ( S ) 3 альбома · 26 треков · ~1ч 38мин
       [▶ Слушать всё]  [🔀 Перемешать]

Альбомы (3)
┌────────┐ ┌────────┐ ┌────────┐
│ Heroes │ │ Last…  │ │ War to │
│  2014  │ │  2016  │ │  2022  │
└────────┘ └────────┘ └────────┘

Прочие треки (2)
▶ Bismarck                    5:55
▶ Steel Commanders            4:18
```

- «← Все исполнители» — back.
- Avatar 70×70 круглый, буква 28px (тот же hash → palette цвет).
- Имя 18px жирно. Счётчики приглушённо.
- Две кнопки: «▶ Слушать всё» (вся дискография как очередь, порядок: альбомы по Year DESC, внутри — TrackNumber ASC, потом «Прочие треки» по Title ASC) и «🔀 Перемешать».
- Секция **Альбомы (N)** — мини-grid 3×N с той же плиткой, что в Albums view. Клик по плитке = drill ещё глубже (AlbumDetailViaArtist).
- Секция **Прочие треки (M)** — только если есть треки с пустым `Album`-тегом. Тот же стиль строк, что в Album detail.
- Если у исполнителя 0 альбомов и >0 прочих треков — Альбомы-секция не показывается, только прочие.
- Если у исполнителя 1 альбом и 0 прочих — секция Альбомы видна с 1 плиткой; не делаем автоматический skip (пользователь должен понять структуру).

**Esc** = back на Artists list (или на Album detail, если drilled через Artist → Album).

## Фильтр-семантика

`LibraryFilter` (уже есть с 1.0.3) применяется ПЕРВЫМ ко всему `_allTracks`. Результат — отфильтрованный `IReadOnlyList<Track>`.

- **Tracks view** — отфильтрованный список как есть в `DisplayedTracks`.
- **Albums view** — отфильтрованные треки группируются в `LibraryGroupingService.GroupByAlbum()`. Альбом виден, если у него есть хотя бы один трек после фильтра.
- **Artists view** — `LibraryGroupingService.GroupByArtist()`. Исполнитель виден, если есть хотя бы один трек.
- **Album detail** — треклист = отфильтрованные треки этого альбома. Поставил фильтр «рейтинг ≥ 4», открыл альбом — видишь только лайкнутые. Это важная семантика: drill-down УВАЖАЕТ активные фильтры.
- **Artist detail** — аналогично: мини-grid альбомов и прочие треки = отфильтрованные. Если фильтр оставил у артиста 0 альбомов, исполнитель просто не виден в Artists list (group дропнут).

При смене фильтра — back-стек сохраняется, контент пересчитывается. Если активный drill-down оказался «пустым» из-за фильтра — показываем empty state: «Под фильтр не попало ни одного трека этого альбома».

## Доменные изменения

### `MusicBakh.Core.Domain.Track`

Добавляются три поля:
```csharp
public int? Year { get; init; }          // год альбома (из ID3)
public int? TrackNumber { get; init; }   // позиция трека в альбоме (из ID3)
public string? AlbumArtist { get; init; } // для compilations: общий исполнитель альбома
```

Все три nullable: старые треки получают NULL, новый импорт заполняет из тегов.

### EF Core миграция `AddTrackYearNumberAlbumArtist`

```sql
ALTER TABLE Tracks ADD COLUMN Year INTEGER NULL;
ALTER TABLE Tracks ADD COLUMN TrackNumber INTEGER NULL;
ALTER TABLE Tracks ADD COLUMN AlbumArtist TEXT NULL;
CREATE INDEX IX_Tracks_Year ON Tracks(Year);
-- TrackNumber индекс не нужен (он внутри одного альбома, сортировка в памяти).
-- AlbumArtist индекс не нужен пока (читается только в группировке).
```

`TrackEntity`, `TrackEntityConfiguration`, маппинги в `SqliteTrackRepository`/`SqliteListeningHistoryRepository`/`SqliteFtsSearchService` — все три поля пробрасываются (по аналогии с тем, как `Album` пробрасывался в 1.0.2 и `Rating`/`Reaction` в 1.0.3).

### Импорт

`LocalTagInfo`, `ResolvedMetadata`, `TrackImportCandidate`, `TagLibSharpTagReader.Read()`, `DefaultMetadataResolver.ResolveAsync()`, `TrackImporter`, `MainViewModel.OpenAddTrackDialog()` — все цепочки пробрасывают `Year`/`TrackNumber`/`AlbumArtist`.

TagLib# поля: `file.Tag.Year`, `file.Tag.Track`, `file.Tag.FirstAlbumArtist` (или `JoinedAlbumArtists` если первый пуст).

## Группировка: `LibraryGroupingService`

Pure-функция в `MusicLibrary/Services/Library/LibraryGroupingService.cs` (рядом с `LibraryFilter`):

```csharp
public static class LibraryGroupingService
{
    public static IReadOnlyList<AlbumAggregate> GroupByAlbum(IReadOnlyList<Track> tracks);
    public static IReadOnlyList<ArtistAggregate> GroupByArtist(IReadOnlyList<Track> tracks);
}
```

### `AlbumAggregate` (record в `MusicLibrary/Services/Library/`)

```csharp
public sealed record AlbumAggregate(
    string Title,
    string Artist,           // = AlbumArtist ?? Artist
    int? Year,               // максимальный Year из треков (фикс на расхождения)
    string CoverPath,        // обложка первого трека по Track.Id
    IReadOnlyList<Track> Tracks); // отсортированные по TrackNumber ASC NULLS LAST, Title ASC

public string AlbumKey => Artist + ((char)0x1F) + Title; // уникальный ID для drill-down state; разделитель U+001F исключает коллизии Artist+Title
```

Сортировка `Tracks` внутри агрегата — детерминированная, в момент построения.

### `ArtistAggregate`

```csharp
public sealed record ArtistAggregate(
    string Name,
    IReadOnlyList<AlbumAggregate> Albums,    // отсортированы Year DESC NULLS LAST, Title ASC
    IReadOnlyList<Track> LooseTracks,        // треки без Album, отсортированы Title ASC
    int TotalTracks,                          // Sum(Albums.Tracks.Count) + LooseTracks.Count
    TimeSpan TotalDuration);
```

### Compilations и AlbumArtist

При группировке альбомов ключ — `(AlbumArtist ?? Artist, Album)`. Если в файлах разных исполнителей одинаковый `AlbumArtist="Various Artists"` и `Album="Greatest Hits 1990"` — альбом склеивается в один объект с `Artist="Various Artists"`, треклист содержит все исполнители.

При группировке исполнителей ключ — `Track.Artist` (НЕ `AlbumArtist`). То есть в Artists view каждый отдельный исполнитель из compilation виден сам по себе со своими треками. Это нужный кейс: пользователь хочет увидеть все треки конкретного артиста, в том числе те, что в чужих compilations.

## ViewModel

Состояние:
```csharp
public enum MainViewMode { Tracks, Albums, Artists }

public MainViewMode ActiveView { get; set; } // bindable, сохраняется в KeyValueStore

// Drill-down state
public Stack<LeftColumnState> NavigationStack { get; } // private
public LeftColumnState CurrentLeftColumn { get; } // computed: stack.Peek() or root by ActiveView

// Aggregates — computed-кэш (пересчитывается при изменении DisplayedTracks)
public IReadOnlyList<AlbumAggregate> DisplayedAlbums { get; }
public IReadOnlyList<ArtistAggregate> DisplayedArtists { get; }

// Drill-down state
public AlbumAggregate? CurrentAlbum { get; }
public ArtistAggregate? CurrentArtist { get; }
```

`DisplayedAlbums`/`DisplayedArtists` пересчитываются как часть `ApplyFilters()` (или в новом `ApplyView()`-методе) — после фильтрации `_allTracks` запускаем оба `GroupByAlbum`/`GroupByArtist` и кэшируем результаты. Стоимость — ~50ms для 50k треков (LINQ GroupBy + Take(N) на сортировке), приемлемо.

Команды:
- `SwitchViewCommand(MainViewMode)` — меняет `ActiveView`, обнуляет стек.
- `OpenAlbumCommand(AlbumAggregate)` — push AlbumDetail в стек.
- `OpenArtistCommand(ArtistAggregate)` — push ArtistDetail.
- `BackCommand` — pop стека (если пустой — no-op).
- `PlayAlbumCommand(AlbumAggregate)` — заменить queue и старт.
- `PlayArtistCommand(ArtistAggregate)` — то же для дискографии артиста.
- `ShuffleAlbumCommand` / `ShuffleArtistCommand` — то же с shuffle.

## XAML-структура

Левая колонка использует **`ContentControl` с DataTemplateSelector** (или один большой Style.Triggers на ActiveView/CurrentLeftColumn). Каждое состояние имеет собственный DataTemplate:

- `TracksViewTemplate` — текущий ListBox (без изменений, переехал в шаблон).
- `AlbumsViewTemplate` — ItemsControl с UniformGrid + AlbumTileTemplate.
- `ArtistsViewTemplate` — ItemsControl с StackPanel + ArtistRowTemplate.
- `AlbumDetailTemplate` — back + header + tracks list.
- `ArtistDetailTemplate` — back + header + Albums section + Loose tracks section.

DataTemplates лежат в новом `MusicLibrary/Resources/AlbumsArtistsTemplates.xaml`. Регистрируются в `App.xaml`.

## Хоткеи

Новые:
- `Ctrl+1` — Tracks tab
- `Ctrl+2` — Albums tab
- `Ctrl+3` — Artists tab
- `Esc` (когда в drill-down) — back

Esc уже занят `SearchBox`-ом для очистки поля; если поиск активен (фокус в поле) — Esc очищает поиск; иначе — выполняет back. Реализуется через приоритет обработки события в `SearchBox.KeyDown` → `Window.KeyDown`.

## Сознательные ограничения (в `scope-deviations.md` §1.0.4)

- **Дубли альбомов** из-за расхождений в написании тегов остаются — см. ADR-0001. Ручное слияние альбомов отложено.
- **Compilations** работают **только** если в тегах правильно заполнен `AlbumArtist`. Файлы без этого тега — каждый исполнитель сам по себе.
- **Биография артиста** не показывается — `1.3.0`-фича (Metadata & Lyrics).
- **Обложка артиста** не загружается — только круглый аватар с инициалом из палитры.
- **Year сортировка** показывает NULL последними, без UX-настройки порядка.
- **Виртуализация ListBox** в Albums/Artists views НЕ вводится в `1.0.4` — на 50k треках количество альбомов ~1000–3000, виртуализации хватает встроенной WPF. Если 100k+ треков начнут лагать — отдельная задача в `1.0.5+`.
- **Right-click context menus** (например, «играть следующим», «удалить альбом») не вводятся — отдельная UX-задача.
- **Drag-and-drop** треков между альбомами/в очередь — не вводится.
- **`AlbumDetailViaArtist`** возвращается на Artist detail (back-стек), а не на Albums grid. Это слегка ассиметрично с прямым Album detail (откуда back на Albums grid). Сознательно — это и есть смысл back-стека.

## Тестирование

- Pure-функции `LibraryGroupingService.GroupByAlbum`/`GroupByArtist` — unit-тесты на:
  - Пустой вход → пустой выход.
  - Группировка по `(AlbumArtist ?? Artist, Album)` для compilations.
  - Сортировка треков внутри альбома по `TrackNumber ASC NULLS LAST, Title ASC`.
  - Сортировка альбомов по `Year DESC NULLS LAST, Title ASC`.
  - Сортировка исполнителей по `Tracks.Count DESC, Name ASC`.
  - `ArtistAggregate.LooseTracks` содержит только треки с `string.IsNullOrEmpty(Album)`.
- ViewModel: тесты на `SwitchViewCommand` + back-стек.
- Импорт: тест на проброс `Year`/`TrackNumber`/`AlbumArtist` (extension к `DefaultMetadataResolverTests`).
- Repository round-trip: новый тест в `SqliteTrackRepositoryTests` (по аналогии с Rating/Reaction).
- Smoke бенчмарк: с 50k треков группировка + рендер не превышает 200ms.

## Definition of Done

- Все 3 tabs кликабельны, переключают контент левой колонки.
- Активный tab сохраняется между перезапусками.
- Albums grid показывает плитки 3×N с обложками и метаданными.
- Artists list показывает аватары + имена + счётчики.
- Drill-down работает в обоих направлениях, back-стек корректен включая Artist → Album.
- Фильтры (search/genre/tags/rating/reaction) применяются и к группам, и к треклистам внутри drill-down.
- Compilations с `AlbumArtist` группируются в один альбом.
- Импорт нового трека читает `Year`/`TrackNumber`/`AlbumArtist` из ID3 и сохраняет в БД.
- Существующие 213 тестов проходят + ~15–20 новых.
- Бенчмарк FTS не просел.
- Apgrade с 1.0.3: миграция `AddTrackYearNumberAlbumArtist` накатывается без потери данных.
