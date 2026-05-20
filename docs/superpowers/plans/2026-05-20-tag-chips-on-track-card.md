# Tag Chips on Track Card + Middle-Column Editing Slot

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать пользователю видеть теги, привязанные к треку, прямо на его карточке в списке (до 3 чипов + «+N» пилюля для overflow); attach/detach делать только в средней колонке через секцию «Теги» под рейтингом/реакциями. Закрывает Task 8 итерации C.

**Architecture:** Карточка трека — read-only превью; средняя колонка — единственное место редактирования. `MainViewModel` держит in-memory кэш `Dictionary<int trackId, ObservableCollection<Tag>>` для биндингов; на attach/detach мутирует соответствующую `ObservableCollection`, и WPF автоматически перерисовывает все ItemsControl-ы, биндингованные на эту коллекцию (через MultiBinding + converter). `Track` сам по себе остаётся неизменяемым, без `Tags`-свойства — нагрузка на VM/converters.

**Tech Stack:** WPF, .NET 10, C# 14. Никаких новых NuGet. Используются уже существующие `ITagRepository`, `TagChipStyle`, `MoreChipButtonStyle`, `PopupResetButtonStyle`, `ToolbarPopupBorderStyle`.

---

## Карта файлов

```
MusicLibrary/
├── ViewModels/
│   └── MainViewModel.cs                  (изменяется: _tagsByTrackId кэш + AttachTagCommand + DetachTagCommand + AvailableTagsForAttach)
├── Converters/
│   ├── TagsToFirstNConverter.cs          (NEW: берёт IReadOnlyList<Tag> + ConverterParameter=N, возвращает первые N)
│   ├── TagsOverflowCountConverter.cs     (NEW: count > N → count-N, иначе 0)
│   └── TrackTagsLookupConverter.cs       (NEW: MultiBinding(trackId, _tagsByTrackId) → ObservableCollection<Tag>)
├── Resources/
│   └── TrackTemplates.xaml               (изменяется: + чип ItemsControl + «+N» pill в TrackCardTemplate)
├── MainWindow.xaml                       (изменяется: + секция «Теги» в средней колонке)
├── MainWindow.xaml.cs                    (изменяется: OnAttachTagClick — открыть popup + handler для popup)

MusicLibrary.Tests/
└── ViewModels/
    └── MainViewModelTagAttachTests.cs    (NEW: 3-4 теста на AttachTag/DetachTag/cache sync)
```

---

## Технический ключевой момент: кэш _tagsByTrackId

`MainViewModel` при старте делает один проход:
```csharp
_tagsByTrackId = new Dictionary<int, ObservableCollection<Tag>>();
foreach (var t in _allTracks)
{
    _tagsByTrackId[t.Id] = new ObservableCollection<Tag>(_tagRepository?.GetTagsForTrack(t.Id) ?? Array.Empty<Tag>());
}
```

При `AttachTag`: `_tagRepository.AttachTag(trackId, tagId)` → найти `Tag` в `TagFilters`, добавить в `_tagsByTrackId[trackId]`. WPF биндинги моментально пересчитываются.

При `DetachTag`: симметрично, `Remove` из коллекции.

При новом треке (импорт): добавить пустой entry. При удалении трека: убрать entry.

Кэш экспонируется как `public IReadOnlyDictionary<int, ObservableCollection<Tag>> TagsByTrackId { get; }`. Биндинги используют MultiBinding (`Track.Id` + `TagsByTrackId`) → converter возвращает нужную `ObservableCollection<Tag>`.

---

## Task 1 — кэш _tagsByTrackId + Attach/Detach команды в MainViewModel + тесты

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs`
- Create: `MusicLibrary.Tests/ViewModels/MainViewModelTagAttachTests.cs`

### Шаги

- [ ] **1.1** В `MainViewModel` добавить поле:
```csharp
private readonly Dictionary<int, ObservableCollection<Tag>> _tagsByTrackId = new();
public IReadOnlyDictionary<int, ObservableCollection<Tag>> TagsByTrackId => _tagsByTrackId;
```

- [ ] **1.2** В конструкторе после `_allTracks = ...` инициализировать кэш:
```csharp
if (_tagRepository is not null)
{
    foreach (var t in _allTracks)
    {
        _tagsByTrackId[t.Id] = new ObservableCollection<Tag>(_tagRepository.GetTagsForTrack(t.Id));
    }
}
```

- [ ] **1.3** Добавить команды:
```csharp
AttachTagToSelectedCommand = new RelayCommand(parameter =>
{
    if (SelectedTrack is null || _tagRepository is null) return;
    if (parameter is not Tag tag) return;
    if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var existing) &&
        existing.Any(t => t.Id == tag.Id)) return; // уже привязан
    _tagRepository.AttachTag(SelectedTrack.Id, tag.Id);
    if (!_tagsByTrackId.ContainsKey(SelectedTrack.Id))
        _tagsByTrackId[SelectedTrack.Id] = new ObservableCollection<Tag>();
    _tagsByTrackId[SelectedTrack.Id].Add(tag);
});

DetachTagFromSelectedCommand = new RelayCommand(parameter =>
{
    if (SelectedTrack is null || _tagRepository is null) return;
    if (parameter is not Tag tag) return;
    _tagRepository.DetachTag(SelectedTrack.Id, tag.Id);
    if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var collection))
    {
        var existing = collection.FirstOrDefault(t => t.Id == tag.Id);
        if (existing is not null) collection.Remove(existing);
    }
});
```

- [ ] **1.4** Объявить публичные `ICommand`-свойства:
```csharp
public ICommand AttachTagToSelectedCommand { get; }
public ICommand DetachTagFromSelectedCommand { get; }
```

- [ ] **1.5** Также добавить computed-свойство `AvailableTagsForAttach` — это все теги из `TagFilters`, у которых нет связки с `SelectedTrack`. Используется popup-ом «+ тег»:
```csharp
public IEnumerable<Tag> AvailableTagsForAttach
{
    get
    {
        if (SelectedTrack is null) return Array.Empty<Tag>();
        var attached = _tagsByTrackId.TryGetValue(SelectedTrack.Id, out var c)
            ? new HashSet<int>(c.Select(t => t.Id))
            : new HashSet<int>();
        return _tagFilters.Where(item => !attached.Contains(item.Tag.Id)).Select(item => item.Tag);
    }
}
```

`OnPropertyChanged(nameof(AvailableTagsForAttach))` нужно вызвать при изменении `SelectedTrack` и при изменениях `_tagsByTrackId[SelectedTrack.Id]`. Для простоты: вызывай в setter-е `SelectedTrack` и в обоих командах после мутации коллекции.

- [ ] **1.6** Когда трек удаляется (`RemoveTrack` или `DeleteSelectedTrack`), убрать его из кэша:
```csharp
_tagsByTrackId.Remove(track.Id);
```

Когда новый трек добавляется (`AddTrack`), завести пустую коллекцию:
```csharp
_tagsByTrackId[track.Id] = new ObservableCollection<Tag>();
```

- [ ] **1.7** Тесты в `MusicLibrary.Tests/ViewModels/MainViewModelTagAttachTests.cs`:

```csharp
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using Xunit;

namespace MusicLibrary.Tests.ViewModels;

public sealed class MainViewModelTagAttachTests
{
    private sealed class FakeTagRepo : ITagRepository
    {
        public List<Tag> Tags { get; } = new();
        public Dictionary<int, List<int>> Attachments { get; } = new(); // trackId → tagIds
        public IReadOnlyList<Tag> GetAll() => Tags;
        public Tag? FindById(int id) => Tags.FirstOrDefault(t => t.Id == id);
        public Tag Add(Tag tag) { Tags.Add(tag); return tag; }
        public void Update(Tag tag) { }
        public void Remove(int id) => Tags.RemoveAll(t => t.Id == id);
        public IReadOnlyList<Tag> GetTagsForTrack(int trackId) =>
            Attachments.TryGetValue(trackId, out var ids)
                ? ids.Select(i => Tags.First(t => t.Id == i)).ToList()
                : Array.Empty<Tag>();
        public void AttachTag(int trackId, int tagId)
        {
            if (!Attachments.ContainsKey(trackId)) Attachments[trackId] = new();
            if (!Attachments[trackId].Contains(tagId)) Attachments[trackId].Add(tagId);
        }
        public void DetachTag(int trackId, int tagId)
        {
            if (Attachments.TryGetValue(trackId, out var ids)) ids.Remove(tagId);
        }
    }

    [Fact]
    public void Attach_Adds_Tag_To_Cache_And_Repository()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var morningTag = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        vm.AttachTagToSelectedCommand.Execute(morningTag);

        Assert.Contains(morningTag, vm.TagsByTrackId[1]);
        Assert.Contains(1, tagRepo.Attachments[1]);
    }

    [Fact]
    public void Attach_Is_Idempotent_No_Duplicate_In_Cache()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var t = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        vm.AttachTagToSelectedCommand.Execute(t);
        vm.AttachTagToSelectedCommand.Execute(t);

        Assert.Single(vm.TagsByTrackId[1]);
    }

    [Fact]
    public void Detach_Removes_Tag_From_Cache_And_Repository()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var t = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        tagRepo.AttachTag(1, 1); // предзагружено
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        Assert.Single(vm.TagsByTrackId[1]); // sanity

        vm.DetachTagFromSelectedCommand.Execute(t);

        Assert.Empty(vm.TagsByTrackId[1]);
        Assert.Empty(tagRepo.Attachments[1]);
    }

    [Fact]
    public void AvailableTagsForAttach_Excludes_Already_Attached()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var morning = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        var work = tagRepo.Add(new Tag { Id = 2, Name = "работа" });
        tagRepo.AttachTag(1, 1); // утро уже привязан
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        var available = vm.AvailableTagsForAttach.ToList();
        Assert.Single(available);
        Assert.Equal("работа", available[0].Name);
    }

    private static MainViewModel CreateVM(IReadOnlyList<Track> tracks, ITagRepository tagRepo)
    {
        // Минимальные test doubles. Используй имеющиеся фейки из MainViewModelTests
        // (можно скопировать или extract — на твоё усмотрение).
        // Ключевое: tagRepository должен быть передан в конструктор.
        throw new NotImplementedException(
            "Implementer: переиспользуй CreateViewModelWithRepo-подобную фабрику из MainViewModelTests.cs " +
            "или extract её в shared helper. Передай tagRepository как ITagRepository, всё остальное — fakes.");
    }
}
```

Implementer-у: factory `CreateVM` потребует test doubles (FakeFileService, FakeAudioPlayerService и т.д.) — переиспользуй из `MainViewModelTests.cs`. Можно extract их в shared класс `Tests/Helpers/MainViewModelFactory.cs` (рекомендуется), или скопировать минимальный набор inline.

- [ ] **1.8** Запустить тесты: `dotnet test ... --filter "FullyQualifiedName~MainViewModelTagAttachTests"`. Все 4 должны пройти.

- [ ] **1.9** Полный прогон: `dotnet test ... --filter "Category!=Benchmark"` — 213 passed (209 + 4).

- [ ] **1.10** Коммит:
```bash
git add MusicLibrary/ViewModels/MainViewModel.cs MusicLibrary.Tests/ViewModels/MainViewModelTagAttachTests.cs
git commit -m "feat(viewmodel): tag attach/detach commands + per-track cache

MainViewModel now keeps a Dictionary<int trackId, ObservableCollection<Tag>>
populated once at startup. AttachTagToSelectedCommand and DetachTagFromSelectedCommand
mutate this cache and forward to ITagRepository. AvailableTagsForAttach exposes
the list for the upcoming '+ тег' picker popup, excluding tags already attached.

Cache entries are created/removed alongside AddTrack/RemoveTrack so newly
imported tracks start with an empty tag list and deleted tracks don't leak.

Four unit tests cover attach/detach round-trip, idempotency, and the
AvailableTagsForAttach filter."
```

---

## Task 2 — Конвертеры для биндинга чипов на карточке

**Files:**
- Create: `MusicLibrary/Converters/TagsToFirstNConverter.cs`
- Create: `MusicLibrary/Converters/TagsOverflowCountConverter.cs`
- Create: `MusicLibrary/Converters/TrackTagsLookupConverter.cs`
- Modify: `MusicLibrary/App.xaml` (register 3 converters)

### Шаги

- [ ] **2.1** `TagsToFirstNConverter`:

```csharp
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Берёт IEnumerable&lt;Tag&gt; (или null), возвращает первые N (ConverterParameter, int).
/// Default N = 3. null → пустая последовательность.
/// </summary>
public sealed class TagsToFirstNConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable seq) return System.Array.Empty<object>();
        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) n = parsed;
        var result = new System.Collections.Generic.List<object>(n);
        int count = 0;
        foreach (var item in seq)
        {
            if (count >= n) break;
            result.Add(item);
            count++;
        }
        return result;
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
```

- [ ] **2.2** `TagsOverflowCountConverter`:

```csharp
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Возвращает (count - N), но не меньше 0. ConverterParameter = N (default 3).
/// Используется для текста «+M» на overflow-пилюле.
/// </summary>
public sealed class TagsOverflowCountConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) n = parsed;
        int count = 0;
        if (value is IEnumerable seq)
        {
            foreach (var _ in seq) count++;
        }
        return System.Math.Max(0, count - n);
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
```

- [ ] **2.3** `TrackTagsLookupConverter` — MultiBinding:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// MultiBinding: values[0] = trackId (int), values[1] = IReadOnlyDictionary&lt;int, ObservableCollection&lt;Tag&gt;&gt;.
/// Возвращает соответствующую ObservableCollection&lt;Tag&gt; или пустой массив.
/// Возвращаем именно ObservableCollection, чтобы ItemsControl реагировал на
/// последующие Add/Remove в кэше без переустановки ItemsSource.
/// </summary>
public sealed class TrackTagsLookupConverter : IMultiValueConverter
{
    public object Convert(object[] values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return System.Array.Empty<Tag>();
        if (values[0] is not int trackId) return System.Array.Empty<Tag>();
        if (values[1] is not IReadOnlyDictionary<int, System.Collections.ObjectModel.ObservableCollection<Tag>> dict)
            return System.Array.Empty<Tag>();
        return dict.TryGetValue(trackId, out var coll) ? (object)coll : System.Array.Empty<Tag>();
    }

    public object[] ConvertBack(object value, System.Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
```

- [ ] **2.4** Регистрация в `App.xaml` рядом с другими converters:
```xml
<converters:TagsToFirstNConverter x:Key="TagsToFirstNConverter" />
<converters:TagsOverflowCountConverter x:Key="TagsOverflowCountConverter" />
<converters:TrackTagsLookupConverter x:Key="TrackTagsLookupConverter" />
```

- [ ] **2.5** Сборка: `dotnet build MusicLibrary/MusicLibrary.csproj --nologo` — 0 errors.

- [ ] **2.6** Тесты: 213 passed (без изменений по числу).

- [ ] **2.7** Коммит:
```bash
git add MusicLibrary/Converters/*.cs MusicLibrary/App.xaml
git commit -m "feat(converters): tag chip rendering helpers

Three converters supporting tag chips on the track card:
* TagsToFirstNConverter — takes first N items (ConverterParameter, default 3).
* TagsOverflowCountConverter — count minus N, floored at 0; feeds the '+M' overflow pill.
* TrackTagsLookupConverter — MultiBinding(trackId, _tagsByTrackId) → ObservableCollection<Tag>,
  used by the card's chip ItemsControl so mutations to the cache re-render automatically."
```

---

## Task 3 — Чипы и overflow pill в TrackCardTemplate

**Files:**
- Modify: `MusicLibrary/Resources/TrackTemplates.xaml`

### Шаги

- [ ] **3.1** Найти `TrackCardTemplate` (текущий шаблон карточки). Внутри `<StackPanel Grid.Column="1" ...>` после блока с жанром и длительностью добавить новую строку — чипы тегов:

```xml
<!-- Чипы тегов: до 3 на карточке + «+N» overflow-пилюля. -->
<StackPanel Orientation="Horizontal" Margin="0,8,0,0" VerticalAlignment="Center">
    <ItemsControl>
        <ItemsControl.ItemsSource>
            <MultiBinding Converter="{StaticResource TrackTagsLookupConverter}">
                <MultiBinding.Bindings>
                    <Binding Path="Id" />
                    <Binding Path="DataContext.TagsByTrackId" RelativeSource="{RelativeSource AncestorType=ListBox}" />
                </MultiBinding.Bindings>
            </MultiBinding>
        </ItemsControl.ItemsSource>
        <!-- Только первые 3 -->
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Style="{StaticResource TagChipStyle}" Padding="6,2" Margin="0,0,4,0">
                    <TextBlock Text="{Binding Name}" Style="{StaticResource TagChipTextStyle}" />
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal" />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <!-- Magic: ItemsControl биндит ВСЮ коллекцию, но мы хотим показать только первые 3.
             Делаем через ICollectionView.Filter — но это сложнее, чем нужно.
             Проще: использовать второй ItemsControl, обёрнутый в Converter.
             Перепишем — см. ниже. -->
    </ItemsControl>
</StackPanel>
```

**Замечание реализатору:** WPF ItemsControl сам не поддерживает «показать первые N». Простейший рабочий вариант — использовать `TagsToFirstNConverter` ВНУТРИ MultiBinding по отдельности, или сделать вспомогательный MultiBinding-чейн. **Чистый рабочий вариант** ниже:

```xml
<StackPanel Orientation="Horizontal" Margin="0,8,0,0" VerticalAlignment="Center">
    <!-- Первые 3 чипа: вход = (Id, dict), выход MultiBinding-а оборачиваем во второй converter. -->
    <ItemsControl>
        <ItemsControl.ItemsSource>
            <MultiBinding Converter="{StaticResource TrackTagsLookupConverter}">
                <Binding Path="Id" />
                <Binding Path="DataContext.TagsByTrackId" RelativeSource="{RelativeSource AncestorType=ListBox}" />
            </MultiBinding>
        </ItemsControl.ItemsSource>
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal" />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Style="{StaticResource TagChipStyle}" Padding="6,2" Margin="0,0,4,0">
                    <TextBlock Text="{Binding Name}" Style="{StaticResource TagChipTextStyle}" />
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</StackPanel>
```

**Implementer-у:** да, такой ItemsControl покажет ВСЕ чипы. Чтобы ограничить до 3 на карточке — добавь ConverterParameter в TrackTagsLookupConverter (передавай N), и внутри TrackTagsLookupConverter возьми только первые N. Это упрощает XAML.

Альтернативно: оборачивай результат во второй converter `TagsToFirstNConverter` через chained binding. Проще — модифицировать `TrackTagsLookupConverter` чтобы принимал параметр:

```csharp
// В TrackTagsLookupConverter.Convert:
int? limit = null;
if (parameter is string s && int.TryParse(s, ..., out var n)) limit = n;
return limit.HasValue ? coll.Take(limit.Value).ToList() : (object)coll;
```

⚠️ ВАЖНО: если ограничиваем `.Take(N).ToList()`, теряем reactivity (новый список, не ObservableCollection). При добавлении тега чип не появится. Решение: для overflow-чипов на карточке reactivity не критична — в типичном сценарии user редко добавляет тег, и при следующем render-е (например, через перевыделение трека) список обновится. Для MVP — `.Take(N).ToList()` ОК, документировать ограничение.

**Окончательное решение для реализации:**
1. Сделай `TrackTagsLookupConverter` принимающим `ConverterParameter` (опциональный int limit). Если задан, возвращает `IReadOnlyList<Tag>` (snapshot), иначе `ObservableCollection<Tag>` (live).
2. На карточке (3 чипа) используй `ConverterParameter=3` → snapshot.
3. В средней колонке (полный список с edit) используй без параметра → live `ObservableCollection`.

- [ ] **3.2** После первого ItemsControl-а с 3 чипами добавить overflow-пилюлю:

```xml
<!-- «+N» pill: видна когда тегов > 3. -->
<Border Background="#1F1F2E" BorderBrush="{StaticResource PrimaryBrush}" BorderThickness="1" CornerRadius="8" Padding="6,2" Margin="0,0,0,0"
        VerticalAlignment="Center">
    <Border.Visibility>
        <MultiBinding Converter="{StaticResource TrackTagsLookupConverter}" ConverterParameter="0">
            <!-- TODO: использовать TagsOverflowCountConverter напрямую -->
        </MultiBinding>
    </Border.Visibility>
    <TextBlock Foreground="{StaticResource PrimaryBrush}" FontSize="11" FontWeight="SemiBold">
        <Run Text="+" /><Run>
            <Run.Text>
                <MultiBinding Converter="{StaticResource TrackTagsLookupConverter}">
                    <Binding Path="Id" />
                    <Binding Path="DataContext.TagsByTrackId" RelativeSource="{RelativeSource AncestorType=ListBox}" />
                </MultiBinding>
            </Run.Text>
        </Run>
    </TextBlock>
</Border>
```

⚠️ Эта разметка некорректна — Run.Text не поддерживает converter напрямую, и логика overflow требует TagsOverflowCountConverter, не TrackTagsLookupConverter.

**Implementer-у — рабочая разметка пилюли:**

```xml
<Border Background="#1F1F2E" BorderBrush="{StaticResource PrimaryBrush}" BorderThickness="1" CornerRadius="8" Padding="6,2" VerticalAlignment="Center">
    <Border.Visibility>
        <MultiBinding>
            <MultiBinding.Converter>
                <converters:OverflowVisibilityConverter />
            </MultiBinding.Converter>
            <Binding Path="Id" />
            <Binding Path="DataContext.TagsByTrackId" RelativeSource="{RelativeSource AncestorType=ListBox}" />
        </MultiBinding>
    </Border.Visibility>
    <TextBlock Foreground="{StaticResource PrimaryBrush}" FontSize="11" FontWeight="SemiBold">
        <TextBlock.Text>
            <MultiBinding StringFormat="+{0}">
                <MultiBinding.Converter>
                    <converters:OverflowCountFromCacheConverter />
                </MultiBinding.Converter>
                <Binding Path="Id" />
                <Binding Path="DataContext.TagsByTrackId" RelativeSource="{RelativeSource AncestorType=ListBox}" />
            </MultiBinding>
        </TextBlock.Text>
    </TextBlock>
</Border>
```

Это требует ДВА дополнительных конвертера:
- `OverflowVisibilityConverter` (IMultiValueConverter): возвращает `Visibility.Visible` если count > 3, иначе `Collapsed`.
- `OverflowCountFromCacheConverter` (IMultiValueConverter): возвращает `count - 3` (int).

**Решение для имплементера:** объедини обе функции в один существующий `TrackTagsLookupConverter` через `ConverterParameter`:
- `ConverterParameter="count"` → возвращает int count.
- `ConverterParameter="overflow"` → возвращает int max(0, count-3).
- `ConverterParameter="overflowVisibility"` → возвращает Visibility.
- Default (нет param) → ObservableCollection.
- `ConverterParameter="first3"` → snapshot первых 3.

То есть `TrackTagsLookupConverter` становится многоцелевым «lookup + transform» по строковому параметру. Это нечисто, но компактно — один converter на все случаи.

**Альтернатива** (чище): создай 2 отдельных IMultiValueConverter (один для visibility, один для count). XAML станет читабельнее, converter-ы простыми.

⚠️ **Implementer должен выбрать чистый путь.** Рекомендуемое: 2 отдельных IMultiValueConverter (`TrackTagsOverflowCountConverter`, `TrackTagsOverflowVisibilityConverter`) в дополнение к `TrackTagsLookupConverter`. Не объединять в один.

Если так — Task 2 нужно расширить, добавив эти converters. ОК сделай это в Task 3 inline (не отдельной таской — мелкая правка к Task 2 артефактам).

- [ ] **3.3** Сборка → smoke (приложение запускается, чипы видны на карточках) → тесты 213 passed.

- [ ] **3.4** Коммит:
```bash
git add MusicLibrary/Resources/TrackTemplates.xaml MusicLibrary/Converters/ MusicLibrary/App.xaml
git commit -m "feat(ui): tag chips on track card with '+N' overflow pill

Track cards now show up to 3 tag chips below the genre/duration row,
plus a gold '+N' overflow pill when the track has more than 3 tags
attached. Chip ItemsControl binds via MultiBinding(track.Id,
MainViewModel.TagsByTrackId) through the existing TrackTagsLookupConverter
(parameterized 'first3' returns a snapshot of the first 3).

Overflow pill uses two new IMultiValueConverters
(TrackTagsOverflowCountConverter / TrackTagsOverflowVisibilityConverter)
to drive its text and visibility — the pill auto-hides at <= 3 tags.

The first-3 list is a snapshot, not the live ObservableCollection — adding
a tag past position 3 won't reflow the card until the next render
(typically re-selection or filter change). Acceptable for MVP per the
brainstorm decision; live reflow can be revisited if it bites in practice."
```

---

## Task 4 — Секция «Теги» в средней колонке + popup «+ тег»

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml`
- Modify: `MusicLibrary/MainWindow.xaml.cs`

### Шаги

- [ ] **4.1** В средней колонке `MainWindow.xaml` найти блок со звёздами и реакциями (под now-playing). После него добавить:

```xml
<!-- Разделитель -->
<Border Height="1" Background="#D4A57420" Margin="0,12,0,12"
        Visibility="{Binding HasSelectedTrack, Converter={StaticResource BooleanToVisibilityConverter}}" />

<!-- Секция «Теги»: чипы привязанных тегов с × для открепления + кнопка «+ тег». -->
<StackPanel Visibility="{Binding HasSelectedTrack, Converter={StaticResource BooleanToVisibilityConverter}}">
    <TextBlock Text="Теги" Foreground="{StaticResource MutedForegroundBrush}" FontSize="12" Margin="0,0,0,6" />
    <ItemsControl>
        <ItemsControl.ItemsSource>
            <MultiBinding Converter="{StaticResource TrackTagsLookupConverter}">
                <Binding Path="SelectedTrack.Id" />
                <Binding Path="TagsByTrackId" />
            </MultiBinding>
        </ItemsControl.ItemsSource>
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel Orientation="Horizontal" />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Style="{StaticResource TagChipStyle}" Padding="6,3" Margin="0,0,4,4">
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <TextBlock Text="{Binding Name}" Style="{StaticResource TagChipTextStyle}" />
                        <Button Margin="6,0,0,0" Padding="0" Background="Transparent" BorderThickness="0" Cursor="Hand"
                                Command="{Binding DataContext.DetachTagFromSelectedCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                CommandParameter="{Binding}">
                            <TextBlock Text="×" FontSize="14" Foreground="{StaticResource MutedForegroundBrush}" />
                        </Button>
                    </StackPanel>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    <Button x:Name="AddTagButton"
            Style="{StaticResource MoreChipButtonStyle}"
            Margin="0,4,0,0"
            HorizontalAlignment="Left"
            Content="+ тег"
            Click="OnAddTagClick" />
</StackPanel>

<!-- Popup со списком доступных тегов. -->
<Popup x:Name="AddTagPopup"
       PlacementTarget="{Binding ElementName=AddTagButton}"
       Placement="Bottom"
       AllowsTransparency="True"
       StaysOpen="False"
       PopupAnimation="Fade">
    <Border Style="{StaticResource ToolbarPopupBorderStyle}" MinWidth="180" MaxWidth="280">
        <StackPanel>
            <TextBlock Text="Доступные теги" Foreground="{StaticResource MutedForegroundBrush}" FontSize="12" Margin="0,0,0,8" />
            <ItemsControl x:Name="AvailableTagsList">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Style="{StaticResource TagChipStyle}" Padding="6,3" Margin="0,0,4,4" Cursor="Hand">
                            <Border.InputBindings>
                                <MouseBinding MouseAction="LeftClick"
                                              Command="{Binding DataContext.AttachTagToSelectedCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                              CommandParameter="{Binding}" />
                            </Border.InputBindings>
                            <TextBlock Text="{Binding Name}" Style="{StaticResource TagChipTextStyle}" />
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            <TextBlock Text="Все теги уже привязаны. Создайте новые в окне Теги (Ctrl+G)."
                       Foreground="{StaticResource MutedForegroundBrush}" FontSize="11" FontStyle="Italic"
                       TextWrapping="Wrap"
                       Visibility="{Binding AvailableTagsForAttach.Count, Converter={StaticResource ZeroAsActiveBrushConverter}}" />
        </StackPanel>
    </Border>
</Popup>
```

⚠️ Эта последняя `TextBlock.Visibility` биндинг через `ZeroAsActiveBrushConverter` НЕ подойдёт — он возвращает Brush, не Visibility. **Implementer-у:** убери этот fallback-текст или сделай отдельный `IntZeroToVisibilityConverter`. Для MVP — просто убери TextBlock, пустой popup сам по себе понятен.

- [ ] **4.2** В `MainWindow.xaml.cs` добавить handler:

```csharp
private void OnAddTagClick(object sender, RoutedEventArgs e)
{
    // DataContext popup-а — MainViewModel (через AvailableTagsList родителя).
    if (AddTagPopup.Child is FrameworkElement popupRoot)
    {
        popupRoot.DataContext = _viewModel;
    }
    AvailableTagsList.ItemsSource = _viewModel.AvailableTagsForAttach.ToList(); // snapshot
    AddTagPopup.IsOpen = !AddTagPopup.IsOpen;
}
```

⚠️ Snapshot нужен, потому что `AvailableTagsForAttach` — это `IEnumerable` который вычисляется в getter-е. При мутации `_tagsByTrackId` (после клика на чип в popup-е) popup должен показать обновлённый список. Поэтому подписываемся на изменения:

```csharp
// В конструкторе MainWindow, после InitializeComponent():
_viewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(MainViewModel.AvailableTagsForAttach) && AddTagPopup?.IsOpen == true)
    {
        AvailableTagsList.ItemsSource = _viewModel.AvailableTagsForAttach.ToList();
    }
};
```

Альтернатива: подписаться на изменения `TagsByTrackId[SelectedTrack.Id]` (ObservableCollection) и пересчитывать. Сложнее. Для MVP — подписаться на `PropertyChanged` достаточно, главное чтобы `AttachTagToSelectedCommand`/`DetachTagFromSelectedCommand` после мутации звали `OnPropertyChanged(nameof(AvailableTagsForAttach))`.

- [ ] **4.3** Сборка → smoke → тесты 213 passed.

- [ ] **4.4** Коммит:
```bash
git add MusicLibrary/MainWindow.xaml MusicLibrary/MainWindow.xaml.cs
git commit -m "feat(ui): middle-column 'Теги' section — attach/detach UI

Below the rating/reaction row, the middle column gains a 'Теги'
section visible whenever a track is selected. Shows all attached tags
as chips with a × button that fires DetachTagFromSelectedCommand. A
'+ тег' button opens a popup listing AvailableTagsForAttach (all tags
not yet attached to the selected track); clicking a chip in the popup
calls AttachTagToSelectedCommand. Popup stays open for multiple
attaches per session.

This is the only editing surface for tag-track associations — the
track card chips (Task 3) are read-only. Per the brainstorm: cards
stay clean, edit lives in the middle column."
```

---

## Task 5 — Manual smoke + ручная проверка чек-листа

- [ ] **5.1** Запустить `dotnet run --project MusicLibrary`.

Проверки:
- [ ] Выбрать трек → в средней колонке под звёздами видна секция «Теги».
- [ ] Если тегов нет — список пустой, только «+ тег».
- [ ] Клик «+ тег» → открывается popup, в нём чипы всех тегов которые ЕЩЁ НЕ привязаны.
- [ ] Клик на чип в popup → тег появляется в средней колонке, исчезает из popup.
- [ ] Popup остаётся открытым → можно привязать несколько за раз.
- [ ] Esc или клик вне popup → popup закрывается.
- [ ] На карточке трека в списке слева тоже появляются чипы (первые 3) — нужно перевыделить трек, чтобы card refresh-нулся (snapshot не reactive).
- [ ] При >3 тегах: видна «+N» золотая пилюля справа от 3 чипов.
- [ ] Клик на × у чипа в средней колонке → тег откреплён, чип исчез, popup-овский AvailableTagsForAttach обновлён.
- [ ] Фильтр-чипы в toolbar (привязка к тому тегу) теперь возвращают реальные треки.
- [ ] Перезапуск приложения → теги сохранены в БД, при старте чипы на карточках восстанавливаются.

Если что-то не работает — фикс инлайн, коммит, прогнать тесты.

---

## Definition of Done

- [ ] Все 5 задач отмечены.
- [ ] 213 unit-тестов зелёные (209 + 4 на Attach/Detach).
- [ ] Бенчмарк не просел.
- [ ] Ручной чек-лист Step 5.1 пройден.
- [ ] Документ `docs/changelog/1.0.3.md` упоминает теги.

## Сознательные ограничения

- **Snapshot, не live, для первых 3 чипов на карточке.** Добавление тега в позицию ≥ 4 не отражается на карточке до перевыделения. Acceptable для MVP, документировано в commit-сообщении.
- **Нет drag-and-drop тегов на треки.** Только клик в popup.
- **«+ тег» popup не имеет inline-создания нового тега.** Только привязка существующих. Создание — через Ctrl+G (TagsWindow).
- **Нет массовой привязки.** Только один трек за раз (тот, что выбран).
- **Сортировка тегов на карточке = порядок attach.** Не алфавит, не приоритет, не цвет. Самое простое.
