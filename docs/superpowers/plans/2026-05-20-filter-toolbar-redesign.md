# Filter Toolbar Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить вертикальную фильтр-панель в левой колонке `MainWindow` на горизонтальный toolbar + popup за иконкой ⋮ согласно [`docs/superpowers/specs/2026-05-20-filter-toolbar-redesign.md`](../specs/2026-05-20-filter-toolbar-redesign.md). Список треков получает обратно вертикальное пространство (цель: ≥7 видимых треков на дефолтном размере).

**Architecture:** Логика фильтрации не меняется — `LibraryFilter` (pure function) и все VM-команды/свойства из Task 7 итерации C остаются. Меняется только Presentation: новая `Grid.Row` между header и колонками; левая колонка теряет фильтр-секцию; новый custom `OverflowChipPanel` для overflow тегов; два `Popup`-а (для «+ N ещё ▾» и для ⋮).

**Tech Stack:** WPF, .NET 10, C# 14. Никаких новых NuGet-пакетов. Custom `Panel` для overflow реализуется как `Panel`-наследник с переопределением `MeasureOverride` / `ArrangeOverride`.

**TDD adaptation:** XAML-разметка не поддаётся unit-тестам в обычном смысле. Для XAML-задач TDD-цикл заменяется на «изменение → сборка → smoke-запуск приложения → визуальная проверка → коммит». Только `OverflowChipPanel` (Task 1) имеет настоящие unit-тесты — он pure C# panel.

---

## Карта файлов

```
MusicLibrary/
├── Controls/
│   └── OverflowChipPanel.cs                  (NEW: custom Panel, overflow chips + "More" pill)
├── MainWindow.xaml                            (изменяется: новый Grid.Row toolbar, удалена фильтр-секция левой колонки)
├── MainWindow.xaml.cs                         (изменяется минимально: handler «Сбросить» если потребуется)
├── Resources/
│   └── ToolbarStyles.xaml                    (NEW: стили под elements toolbar и popup)
└── (App.xaml — изменяется: подключить ToolbarStyles.xaml)

MusicLibrary.Tests/
└── Controls/
    └── OverflowChipPanelTests.cs              (NEW: unit-тесты на measure-поведение)
```

---

## Task 1 — OverflowChipPanel: custom Panel с overflow

Custom `Panel` для контейнера тег-чипов в toolbar. Принимает любое число дочерних элементов. **Последний дочерний элемент трактуется как «More»-пилюля** (это конвенция плагина: чипы — это все остальные children, «+ N ещё ▾» — последний). При нехватке ширины — chip'ы скрываются справа налево, More-пилюля показывается; если все умещаются — More-пилюля скрывается. Считает `HiddenCount` через DependencyProperty, чтобы UI мог биндить количество в текст «+ N ещё ▾».

**Files:**
- Create: `MusicLibrary/Controls/OverflowChipPanel.cs`
- Test: `MusicLibrary.Tests/Controls/OverflowChipPanelTests.cs`

- [ ] **Step 1.1: Создать тестовый файл с базовым набором тестов (FAIL)**

Files: `MusicLibrary.Tests/Controls/OverflowChipPanelTests.cs`

```csharp
using System.Windows;
using System.Windows.Controls;
using MusicLibrary.Controls;
using Xunit;

namespace MusicLibrary.Tests.Controls;

public sealed class OverflowChipPanelTests
{
    // Используем простой UIElement-«пенёк»: фиксированный DesiredSize.
    private sealed class FixedSizeElement : UIElement
    {
        private readonly Size _size;
        public FixedSizeElement(double width, double height = 24) { _size = new Size(width, height); }
        protected override Size MeasureCore(Size availableSize) => _size;
    }

    private static OverflowChipPanel BuildPanel(params double[] chipWidths)
    {
        var panel = new OverflowChipPanel();
        foreach (var w in chipWidths)
        {
            panel.Children.Add(new FixedSizeElement(w));
        }
        // Последний — «More» пилюля шириной 80.
        panel.Children.Add(new FixedSizeElement(80));
        return panel;
    }

    private static void DoLayout(OverflowChipPanel panel, double availableWidth, double availableHeight = 40)
    {
        panel.Measure(new Size(availableWidth, availableHeight));
        panel.Arrange(new Rect(0, 0, availableWidth, availableHeight));
    }

    [Fact]
    public void All_Chips_Fit_More_Pill_Hidden()
    {
        var panel = BuildPanel(60, 60, 60);
        DoLayout(panel, availableWidth: 300);

        Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
        Assert.Equal(Visibility.Visible, panel.Children[1].Visibility);
        Assert.Equal(Visibility.Visible, panel.Children[2].Visibility);
        Assert.Equal(Visibility.Collapsed, panel.Children[3].Visibility); // more pill
        Assert.Equal(0, panel.HiddenCount);
    }

    [Fact]
    public void Some_Chips_Hidden_More_Pill_Visible_HiddenCount_Correct()
    {
        // Чипы по 60, More по 80. Доступно 200 px.
        // Включаем chip 0 (60), chip 1 (120). chip 2 не влезает (180) — пытаемся, но 180 + 80 (more) = 260 > 200,
        // значит надо урезать до chip 0 (60 + 80 = 140 <= 200) — chip 1, chip 2 скрыты.
        var panel = BuildPanel(60, 60, 60);
        DoLayout(panel, availableWidth: 200);

        Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
        Assert.Equal(Visibility.Collapsed, panel.Children[1].Visibility);
        Assert.Equal(Visibility.Collapsed, panel.Children[2].Visibility);
        Assert.Equal(Visibility.Visible, panel.Children[3].Visibility); // more pill
        Assert.Equal(2, panel.HiddenCount);
    }

    [Fact]
    public void Zero_Chips_Renders_Empty_With_Hidden_More()
    {
        var panel = new OverflowChipPanel();
        // Только More-пилюля, без chip'ов.
        panel.Children.Add(new FixedSizeElement(80));
        DoLayout(panel, availableWidth: 200);

        Assert.Equal(Visibility.Collapsed, panel.Children[0].Visibility);
        Assert.Equal(0, panel.HiddenCount);
    }

    [Fact]
    public void Exact_Fit_All_Chips_Visible()
    {
        // Сумма чипов ровно равна доступной ширине, More не нужен.
        var panel = BuildPanel(100, 100);
        DoLayout(panel, availableWidth: 200);

        Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
        Assert.Equal(Visibility.Visible, panel.Children[1].Visibility);
        Assert.Equal(Visibility.Collapsed, panel.Children[2].Visibility);
        Assert.Equal(0, panel.HiddenCount);
    }
}
```

- [ ] **Step 1.2: Запустить тесты — они должны упасть с ошибкой компиляции «OverflowChipPanel not found»**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~OverflowChipPanelTests" --nologo`
Expected: build error, тип `OverflowChipPanel` не существует.

- [ ] **Step 1.3: Реализовать OverflowChipPanel — минимальная имплементация**

Files: `MusicLibrary/Controls/OverflowChipPanel.cs`

```csharp
using System.Windows;
using System.Windows.Controls;

namespace MusicLibrary.Controls;

/// <summary>
/// Контейнер для тег-чипов в фильтр-toolbar. Конвенция: <b>последний child</b> — это
/// «More»-пилюля («+ N ещё ▾»), все остальные — обычные чипы.
///
/// Логика:
/// * Измеряем все чипы и More-пилюлю.
/// * Включаем чипы слева направо, пока сумма ширин помещается.
/// * Если хотя бы один чип не влез — оставляем место под More-пилюлю и при необходимости
///   убираем ещё чипы справа налево, пока More-пилюля помещается.
/// * Скрываем не влезшие через Visibility=Collapsed; show/hide More соответственно.
/// * Записываем количество скрытых в HiddenCount (DP) — XAML биндит на текст «+ N ещё».
/// </summary>
public sealed class OverflowChipPanel : Panel
{
    public static readonly DependencyProperty HiddenCountProperty =
        DependencyProperty.Register(
            nameof(HiddenCount),
            typeof(int),
            typeof(OverflowChipPanel),
            new PropertyMetadata(0));

    public int HiddenCount
    {
        get => (int)GetValue(HiddenCountProperty);
        private set => SetValue(HiddenCountProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        int count = InternalChildren.Count;
        if (count == 0)
        {
            HiddenCount = 0;
            return new Size();
        }

        UIElement morePill = InternalChildren[count - 1];
        var infiniteSlot = new Size(double.PositiveInfinity, constraint.Height);

        // Сначала измеряем все, чтобы знать ширины.
        for (int i = 0; i < count; i++)
        {
            InternalChildren[i].Measure(infiniteSlot);
        }

        double availableWidth = double.IsInfinity(constraint.Width) ? double.MaxValue : constraint.Width;
        double morePillWidth = morePill.DesiredSize.Width;

        // Жадно набираем чипы, пока сумма ≤ availableWidth.
        double used = 0;
        int lastVisibleChip = -1;
        for (int i = 0; i < count - 1; i++)
        {
            double w = InternalChildren[i].DesiredSize.Width;
            if (used + w <= availableWidth)
            {
                used += w;
                lastVisibleChip = i;
            }
            else
            {
                break;
            }
        }

        int hiddenCount = (count - 1) - (lastVisibleChip + 1);

        // Если что-то скрыто — надо оставить место для More-пилюли,
        // при необходимости снимая видимые чипы.
        if (hiddenCount > 0)
        {
            while (lastVisibleChip >= 0 && used + morePillWidth > availableWidth)
            {
                used -= InternalChildren[lastVisibleChip].DesiredSize.Width;
                lastVisibleChip--;
            }
            hiddenCount = (count - 1) - (lastVisibleChip + 1);
        }

        // Применяем Visibility.
        for (int i = 0; i < count - 1; i++)
        {
            InternalChildren[i].Visibility = i <= lastVisibleChip ? Visibility.Visible : Visibility.Collapsed;
        }
        morePill.Visibility = hiddenCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        HiddenCount = hiddenCount;

        double totalWidth = used + (hiddenCount > 0 ? morePillWidth : 0);
        double totalHeight = 0;
        for (int i = 0; i < count; i++)
        {
            if (InternalChildren[i].Visibility == Visibility.Visible)
            {
                totalHeight = System.Math.Max(totalHeight, InternalChildren[i].DesiredSize.Height);
            }
        }

        return new Size(System.Math.Min(totalWidth, availableWidth), totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = InternalChildren.Count;
        double x = 0;
        for (int i = 0; i < count - 1; i++)
        {
            var c = InternalChildren[i];
            if (c.Visibility != Visibility.Visible) continue;
            c.Arrange(new Rect(x, 0, c.DesiredSize.Width, finalSize.Height));
            x += c.DesiredSize.Width;
        }
        if (count > 0)
        {
            var more = InternalChildren[count - 1];
            if (more.Visibility == Visibility.Visible)
            {
                more.Arrange(new Rect(x, 0, more.DesiredSize.Width, finalSize.Height));
            }
        }
        return finalSize;
    }
}
```

- [ ] **Step 1.4: Запустить тесты — все 4 должны пройти**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~OverflowChipPanelTests" --nologo`
Expected: 4 passed.

Если упало — глянь, что мой `MeasureCore`-override в `FixedSizeElement` корректно отдаёт DesiredSize. По умолчанию WPF UIElement.Measure cache'ит Visibility=Collapsed → DesiredSize=Empty. Для теста нужно, чтобы DesiredSize всегда отражал «реальную» ширину.

- [ ] **Step 1.5: Прогон всего сьюта — не сломал ли существующие тесты**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 199 + 4 = 203 passed (199 на момент старта плана).

- [ ] **Step 1.6: Коммит**

```bash
git add MusicLibrary/Controls/OverflowChipPanel.cs MusicLibrary.Tests/Controls/OverflowChipPanelTests.cs
git commit -m "$(cat <<'EOF'
feat(controls): OverflowChipPanel — custom Panel with chip overflow + More pill

Filter toolbar (1.0.3 redesign) needs a horizontal chip container that
hides chips when they don't fit and surfaces a "+ N ещё ▾" pill to open
a popup with the rest. Implements the convention: last child is the
More pill, all others are chips; MeasureOverride greedily fits chips
left-to-right and reserves space for the More pill when any chip is
hidden. Exposes HiddenCount as a DependencyProperty so the More pill's
text can bind to it.

Four unit tests cover: all fit (More hidden), some hidden (More
visible, count correct), zero chips (empty), exact fit (no overflow).
EOF
)"
```

---

## Task 2 — ToolbarStyles.xaml + подключение в App.xaml

Стили под elements toolbar (контейнер, иконка ⋮, пилюли «+ N ещё ▾»). Стили `SearchTextBoxStyle`, `GenreComboBoxStyle`, `TagChipStyle` уже есть и переиспользуются. Здесь — только новое.

**Files:**
- Create: `MusicLibrary/Resources/ToolbarStyles.xaml`
- Modify: `MusicLibrary/App.xaml`

- [ ] **Step 2.1: Написать ToolbarStyles.xaml**

Files: `MusicLibrary/Resources/ToolbarStyles.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Контейнер toolbar — тёмный фон с золотой нижней рамкой, единый padding. -->
    <Style x:Key="FilterToolbarBorderStyle" TargetType="Border">
        <Setter Property="Background" Value="#4D16161F" />
        <Setter Property="BorderBrush" Value="{StaticResource GoldBorderBrush}" />
        <Setter Property="BorderThickness" Value="0,0,0,1" />
        <Setter Property="Padding" Value="18,12" />
    </Style>

    <!-- Иконка ⋮ — узкая кнопка с тёмной рамкой, hover-эффектом. -->
    <Style x:Key="ToolbarIconButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="#1F1F2E" />
        <Setter Property="BorderBrush" Value="{StaticResource GoldBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
        <Setter Property="FontSize" Value="18" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="Width" Value="36" />
        <Setter Property="Height" Value="36" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="6">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="#33D4A574" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Пилюля «+ N ещё ▾» — кликабельная, золотая рамка. -->
    <Style x:Key="MoreChipButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="#1F1F2E" />
        <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="10,4" />
        <Setter Property="Margin" Value="0,0,0,0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="10"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="#33D4A574" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Popup внутри toolbar — тёмный фон, рамка, тень. Используется ⋮ и «+ N ещё ▾». -->
    <Style x:Key="ToolbarPopupBorderStyle" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource BackgroundBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="10" />
        <Setter Property="Padding" Value="18" />
        <Setter Property="Effect">
            <Setter.Value>
                <DropShadowEffect Color="Black" BlurRadius="24" ShadowDepth="6" Opacity="0.5" />
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Кнопка в popup-е (например, «Все» в фильтре рейтинга, или Reset bottom-bar). -->
    <Style x:Key="PopupPillButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="#1F1F2E" />
        <Setter Property="BorderBrush" Value="#D4A57440" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Padding" Value="10,5" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="8"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Полоса-кнопка «Сбросить все фильтры» в нижней части popup ⋮. -->
    <Style x:Key="PopupResetButtonStyle" TargetType="Button" BasedOn="{StaticResource PopupPillButtonStyle}">
        <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryBrush}" />
        <Setter Property="HorizontalAlignment" Value="Stretch" />
        <Setter Property="Padding" Value="10,8" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2.2: Подключить ToolbarStyles.xaml в App.xaml**

Files: `MusicLibrary/App.xaml` — в `MergedDictionaries` после `TabStyles.xaml`, перед `TagChipStyles.xaml`.

```xml
                <ResourceDictionary Source="Resources/TabStyles.xaml" />
                <ResourceDictionary Source="Resources/ToolbarStyles.xaml" />
                <ResourceDictionary Source="Resources/TagChipStyles.xaml" />
```

- [ ] **Step 2.3: Сборка**

Run: `dotnet build MusicLibrary/MusicLibrary.csproj --nologo`
Expected: 0 errors, 0 warnings.

- [ ] **Step 2.4: Коммит**

```bash
git add MusicLibrary/Resources/ToolbarStyles.xaml MusicLibrary/App.xaml
git commit -m "$(cat <<'EOF'
chore(ui): ToolbarStyles.xaml — styles for filter toolbar redesign

Adds five styles needed by the upcoming horizontal filter toolbar:
* FilterToolbarBorderStyle — toolbar container (dark bg, gold underline)
* ToolbarIconButtonStyle — square 36x36 button for the ⋮ trigger
* MoreChipButtonStyle — pill-shaped "+ N ещё ▾" trigger
* ToolbarPopupBorderStyle — drop-shadowed dark container for the popups
* PopupPillButtonStyle / PopupResetButtonStyle — buttons inside popups
EOF
)"
```

---

## Task 3 — Удалить старую фильтр-секцию из MainWindow

Старая разметка (Task 7 итерации C) занимает левую колонку. Удаляем её ДО того, как вставлять toolbar — так разница в diff'е будет читабельнее, и приложение между Task 3 и Task 4 будет в «промежуточном» состоянии (фильтры по жанру, поиск, теги недоступны — это OK, мы их сразу добавим в Task 4).

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml`

- [ ] **Step 3.1: Удалить блоки фильтр-панели в левой колонке**

В файле `MusicLibrary/MainWindow.xaml` найти и удалить:

1. Блок «Поиск» (`<StackPanel DockPanel.Dock="Top" Margin="0,0,0,14">` с TextBlock «Поиск» и TextBox SearchBox).
2. Блок «Фильтр по жанру» (`<StackPanel DockPanel.Dock="Top" Margin="0,0,0,14">` с TextBlock «Фильтр по жанру» и ComboBox).
3. Блок «Дополнительные фильтры» (большой `<StackPanel DockPanel.Dock="Top" Margin="0,0,0,18">` с рейтингом-слайдером, реакциями, тегами и кнопкой «Сбросить»).

После удаления левый `<Border Grid.Column="0">` должен содержать только `<DockPanel Margin="24">` с одним дочерним `<ListBox>` (DisplayedTracks).

**Важно:** убедись, что ВСЕ три блока удалены целиком — особенно «Дополнительные фильтры» большой (~80 строк). После удаления `<DockPanel>` должен сразу содержать `<ListBox ItemsSource="{Binding DisplayedTracks}" ...>` без посредников.

- [ ] **Step 3.2: Сборка**

Run: `dotnet build MusicLibrary/MusicLibrary.csproj --nologo`
Expected: 0 errors. Возможны warning'и про unused styles — игнорируем, восстановим в Task 4.

- [ ] **Step 3.3: Smoke-запуск приложения, чтобы убедиться, что не упало**

Run: `cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: EF-логи запуска (SELECT по KeyValueStore), приложение живёт 6 сек до таймаута.

В этот момент приложение запускается, но левая колонка показывает **только список треков**, фильтры недоступны. Это ожидаемое промежуточное состояние.

- [ ] **Step 3.4: Коммит**

```bash
git add MusicLibrary/MainWindow.xaml
git commit -m "$(cat <<'EOF'
refactor(ui): drop old vertical filter panel from MainWindow left column

Step 1 of the toolbar redesign — remove the Task 7 vertical filter
section to clear the way for the new horizontal toolbar. App still
builds and launches; the left column now shows only the track list.
Search, genre, tag, rating, reaction filters are temporarily
unreachable through the UI — Task 4 wires them into the new toolbar
immediately. ViewModel bindings stay intact, only the View changes.
EOF
)"
```

---

## Task 4 — Toolbar: search + genre + tag chips + ⋮ trigger

Вставляем новый `Grid.Row` между шапкой и тремя колонками. Содержимое: поиск, ComboBox жанра, `OverflowChipPanel` с тег-чипами + кнопкой «+ N ещё ▾», иконка ⋮. Popup-ы наполним в Task 5 и Task 6.

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml`

- [ ] **Step 4.1: Добавить пространство имён controls и обновить Grid.RowDefinitions**

В корне `<Window>`:

```xml
<Window x:Class="MusicLibrary.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:MusicLibrary.Controls"
        ...>
```

В главном `<Grid>` (первый внутри `<Window>`):

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="106" />     <!-- было: header -->
    <RowDefinition Height="Auto" />    <!-- НОВОЕ: toolbar -->
    <RowDefinition Height="*" />       <!-- было: 3 колонки, теперь Row=2 -->
</Grid.RowDefinitions>
```

Изменить `<Grid Grid.Row="1">` (3-колоночный body) на `<Grid Grid.Row="2">`.

- [ ] **Step 4.2: Вставить разметку toolbar между header (Row=0) и колонками (Row=2)**

Между `</Border>` существующего header'а и `<Grid Grid.Row="2">` добавить:

```xml
<Border Grid.Row="1" Style="{StaticResource FilterToolbarBorderStyle}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" MinWidth="180" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="2*" MinWidth="220" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>

        <!-- Поиск -->
        <TextBox x:Name="SearchBox"
                 Grid.Column="0"
                 Style="{StaticResource SearchTextBoxStyle}"
                 Tag="Название, исполнитель, альбом, жанр…   (Ctrl+F)"
                 Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=250}"
                 KeyDown="OnSearchBoxKeyDown"
                 VerticalAlignment="Center" />

        <!-- Жанр -->
        <ComboBox Grid.Column="1"
                  ItemsSource="{Binding Genres}"
                  SelectedItem="{Binding SelectedGenre, Mode=TwoWay}"
                  Style="{StaticResource GenreComboBoxStyle}"
                  MinWidth="140"
                  Margin="14,0,0,0"
                  VerticalAlignment="Center" />

        <!-- Тег-чипы + More-пилюля через OverflowChipPanel -->
        <controls:OverflowChipPanel x:Name="TagChipsPanel"
                                    Grid.Column="2"
                                    Margin="14,0,0,0"
                                    VerticalAlignment="Center">
            <!-- Сами чипы рендерятся через ItemsControl, но OverflowChipPanel
                 ожидает дочерние элементы напрямую (Last = More). Используем
                 ItemsControl как поставщика чипов, оборачивая его так, чтобы
                 каждый элемент попадал в OverflowChipPanel как direct child.
                 Решение: используем OverflowChipPanel как ItemsPanel у ItemsControl
                 и кладём More-пилюлю в отдельный Children-слот через композит.
                 ВАЖНО: рендерим через два уровня:
                 1) ItemsControl с OverflowChipPanel в ItemsPanel
                 2) More-пилюля — отдельным элементом ПОВЕРХ ItemsControl-а
                 НО так не сработает (More должна быть child панели), поэтому
                 строим вручную: ItemsControl рендерит чипы, для overflow
                 используем НЕ ItemsPanel а ручную композицию — см. Step 4.3. -->
        </controls:OverflowChipPanel>

        <!-- ⋮ trigger — Task 5 наполнит popup -->
        <Button Grid.Column="3"
                Style="{StaticResource ToolbarIconButtonStyle}"
                Margin="14,0,0,0"
                Content="⋮"
                Click="OnMoreFiltersClick"
                ToolTip="Рейтинг, реакция, сброс" />
    </Grid>
</Border>
```

**Замечание Step 4.2:** конструкция выше с пустой `OverflowChipPanel` — заглушка. Корректное решение — в Step 4.3 ниже, потому что нам нужно одновременно (a) перебирать `TagFilters` через `ItemsControl` и (b) держать More-пилюлю как последний direct child панели. Удалить псевдо-разметку выше, заменить на правильную.

- [ ] **Step 4.3: Правильная разметка тег-чипов с overflow**

Вместо пустой `OverflowChipPanel` из Step 4.2 использовать `ItemsControl` с `OverflowChipPanel` в роли `ItemsPanel`. More-пилюлю кладём в `ItemsControl.Template` через `ControlTemplate` так, чтобы она была последним child панели. Так:

Замени блок `<controls:OverflowChipPanel ...>...</controls:OverflowChipPanel>` на:

```xml
<ItemsControl Grid.Column="2"
              x:Name="TagChipsHost"
              ItemsSource="{Binding TagFilters}"
              Margin="14,0,0,0"
              VerticalAlignment="Center">
    <ItemsControl.Template>
        <ControlTemplate TargetType="ItemsControl">
            <controls:OverflowChipPanel x:Name="ChipsPanel">
                <!-- Чипы (генерируются ItemsControl-ом) -->
                <ItemsPresenter />
                <!-- More-пилюля — последним child панели, видимостью управляет панель. -->
                <Button x:Name="MorePill"
                        Style="{StaticResource MoreChipButtonStyle}"
                        Click="OnMoreTagsClick">
                    <TextBlock>
                        <Run Text="+ " />
                        <Run Text="{Binding HiddenCount, ElementName=ChipsPanel, Mode=OneWay}" />
                        <Run Text=" ещё ▾" />
                    </TextBlock>
                </Button>
            </controls:OverflowChipPanel>
        </ControlTemplate>
    </ItemsControl.Template>
    <ItemsControl.ItemsPanel>
        <!-- ItemsPanel НЕ используется напрямую — мы переопределили Template.
             Но WPF требует ItemsPanel при наличии ItemsSource; оставляем
             заглушку StackPanel (она не используется, ControlTemplate
             переопределяет рендеринг через ItemsPresenter). -->
        <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Cursor="Hand" Margin="0,0,6,0">
                <Border.InputBindings>
                    <MouseBinding MouseAction="LeftClick"
                                  Command="{Binding DataContext.ToggleTagFilterCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                  CommandParameter="{Binding}" />
                </Border.InputBindings>
                <Border.Style>
                    <Style TargetType="Border" BasedOn="{StaticResource TagChipStyle}">
                        <Setter Property="Margin" Value="0" />
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                <Setter Property="Background" Value="#33D4A574" />
                                <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Border.Style>
                <TextBlock Text="{Binding Tag.Name}" Style="{StaticResource TagChipTextStyle}" />
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**Замечание:** `ItemsPresenter` в `OverflowChipPanel` сам по себе НЕ является «множеством дочерних элементов» — он рендерит контейнеры элементов поверх собственной поверхности. То есть для нашей панели `ItemsPresenter` — это один UIElement (тот, который содержит все чипы). Это сломает логику «last child = More». Поэтому правильная архитектура — НЕ использовать ItemsControl/ItemsPresenter, а написать свой adapter.

**Откорректированный Step 4.3** (отбрасываем подход с ItemsPresenter):

Удали разметку из псевдо-Step 4.3 выше. Вместо неё используй прямую генерацию чипов через `OverflowChipPanel` + bind через code-behind. То есть:

В XAML просто:

```xml
<controls:OverflowChipPanel x:Name="TagChipsPanel"
                            Grid.Column="2"
                            Margin="14,0,0,0"
                            VerticalAlignment="Center">
    <!-- Children'ы наполняются в code-behind: для каждого TagFilters добавляем
         Border-чип, последним — кнопку «+ N ещё ▾». Подписка на изменения
         TagFilters пересобирает Children. -->
</controls:OverflowChipPanel>
```

И в `MainWindow.xaml.cs` синхронизируем `TagChipsPanel.Children` с `VM.TagFilters` через подписку на `INotifyCollectionChanged`. Это вынесем в Step 4.4.

- [ ] **Step 4.4: Code-behind, синхронизирующий OverflowChipPanel.Children с VM.TagFilters**

Files: `MusicLibrary/MainWindow.xaml.cs`

Добавить в `MainWindow`:

```csharp
private System.Collections.Specialized.INotifyCollectionChanged? _tagFiltersSource;

protected override void OnContentRendered(EventArgs e)
{
    base.OnContentRendered(e);
    HookTagFiltersSync();
    RebuildTagChips();
}

private void HookTagFiltersSync()
{
    if (_viewModel.TagFilters is System.Collections.Specialized.INotifyCollectionChanged ncc)
    {
        ncc.CollectionChanged += (_, _) => RebuildTagChips();
        _tagFiltersSource = ncc;
    }
    // Внутри каждого TagFilterItem PropertyChanged на IsSelected тоже
    // влияет на визуал, но IsSelected → Style trigger в шаблоне чипа.
    // Шаблон чипа создаётся в RebuildTagChips через CreateChip.
}

private void RebuildTagChips()
{
    // 1. Снимаем все children, кроме «More»-пилюли (последняя). Если её ещё нет — добавим.
    Button morePill = EnsureMorePill();
    TagChipsPanel.Children.Clear();

    foreach (var item in _viewModel.TagFilters)
    {
        TagChipsPanel.Children.Add(CreateChip(item));
    }
    // More-пилюля кладётся ПОСЛЕДНЕЙ — конвенция OverflowChipPanel.
    TagChipsPanel.Children.Add(morePill);
}

private Button _morePillCached;

private Button EnsureMorePill()
{
    if (_morePillCached is null)
    {
        var tb = new System.Windows.Controls.TextBlock();
        tb.Inlines.Add(new System.Windows.Documents.Run("+ "));
        var countRun = new System.Windows.Documents.Run();
        BindingOperations.SetBinding(countRun, System.Windows.Documents.Run.TextProperty,
            new Binding(nameof(Controls.OverflowChipPanel.HiddenCount))
            {
                Source = TagChipsPanel,
                Mode = BindingMode.OneWay
            });
        tb.Inlines.Add(countRun);
        tb.Inlines.Add(new System.Windows.Documents.Run(" ещё ▾"));

        _morePillCached = new Button
        {
            Style = (Style)FindResource("MoreChipButtonStyle"),
            Content = tb
        };
        _morePillCached.Click += OnMoreTagsClick;
    }
    return _morePillCached;
}

private System.Windows.Controls.Border CreateChip(MusicLibrary.ViewModels.TagFilterItem item)
{
    var chipBorder = new System.Windows.Controls.Border
    {
        Style = (Style)FindResource("TagChipStyle"),
        Cursor = System.Windows.Input.Cursors.Hand,
        DataContext = item
    };
    // Trigger «IsSelected → подсветка» — через локальный Style override.
    var styleOverride = new Style(typeof(System.Windows.Controls.Border),
        (Style)FindResource("TagChipStyle"));
    var trigger = new DataTrigger
    {
        Binding = new Binding(nameof(MusicLibrary.ViewModels.TagFilterItem.IsSelected)),
        Value = true
    };
    trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty,
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xD4, 0xA5, 0x74))));
    trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BorderBrushProperty,
        (System.Windows.Media.Brush)FindResource("PrimaryBrush")));
    styleOverride.Triggers.Add(trigger);
    chipBorder.Style = styleOverride;
    chipBorder.Margin = new Thickness(0, 0, 6, 0);

    chipBorder.MouseLeftButtonUp += (_, _) => _viewModel.ToggleTagFilterCommand.Execute(item);

    var text = new System.Windows.Controls.TextBlock
    {
        Text = item.Tag.Name,
        Style = (Style)FindResource("TagChipTextStyle")
    };
    chipBorder.Child = text;
    return chipBorder;
}

private void OnMoreTagsClick(object sender, RoutedEventArgs e)
{
    // Task 6 наполнит логику. Пока — просто плейсхолдер.
}

private void OnMoreFiltersClick(object sender, RoutedEventArgs e)
{
    // Task 5 наполнит логику. Пока — плейсхолдер.
}
```

Добавить `using System.Windows.Data;` и `using System.Windows.Controls;` если их нет.

- [ ] **Step 4.5: Сборка + smoke-запуск**

Run:
```bash
dotnet build MusicLibrary/MusicLibrary.csproj --nologo
cd MusicLibrary && timeout 6 dotnet run --no-build --verbosity quiet 2>&1 | tail -3
```
Expected: build clean. Запуск показывает приложение. Toolbar виден между header и колонками. Поиск, жанр работают. Чипы тегов появляются (если они есть в БД). При сужении окна чипы скрываются за «+ N ещё ▾» (popup пока заглушка). Левая колонка показывает 7+ треков.

- [ ] **Step 4.6: Полный тест-сьют — VM-тесты не должны пострадать**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 203 passed.

- [ ] **Step 4.7: Коммит**

```bash
git add MusicLibrary/MainWindow.xaml MusicLibrary/MainWindow.xaml.cs
git commit -m "$(cat <<'EOF'
feat(ui): horizontal filter toolbar with search/genre/tag chips/⋮ trigger

Inserts a new Grid.Row between the brand header and the 3-column body.
Holds: search box (full SearchTextBoxStyle), genre ComboBox, an
OverflowChipPanel that renders TagFilters as clickable chips with
auto-hiding overflow + "+ N ещё ▾" pill, and a ⋮ icon button that will
trigger the secondary-filters popup in Task 5.

Tag chips are rebuilt in code-behind on OnContentRendered and on
TagFilters CollectionChanged. The More pill is a cached singleton that
binds its visible "N" to OverflowChipPanel.HiddenCount.

Both ⋮ and "+ N ещё ▾" click handlers are placeholders for now —
Tasks 5 and 6 fill in the popup contents. Search, genre, and tag
chip clicks already drive the existing LibraryFilter pipeline.
EOF
)"
```

---

## Task 5 — Popup ⋮: рейтинг звёздами + реакция + Reset

Иконка ⋮ открывает Popup с тремя секциями. Никаких новых VM-команд — все есть со времён Task 7 итерации C (`MinRating`, `ReactionFilter`, `SetReactionFilterCommand`, `ClearFiltersCommand`). Для рейтинга звёздами вводим новую команду `SetMinRatingCommand` (toggle-семантика — клик на горящую звезду сбрасывает в 0) и новый конвертер `ZeroAsActiveBrushConverter` для пилюли «Все».

**Files:**
- Modify: `MusicLibrary/ViewModels/MainViewModel.cs` (новая команда `SetMinRatingCommand`)
- Create: `MusicLibrary/Converters/ZeroAsActiveBrushConverter.cs`
- Modify: `MusicLibrary/App.xaml` (зарегистрировать новый конвертер)
- Modify: `MusicLibrary/MainWindow.xaml` (добавить Popup ⋮)

- [ ] **Step 5.1: Добавить SetMinRatingCommand в MainViewModel**

Files: `MusicLibrary/ViewModels/MainViewModel.cs`

В блок «инициализация команд» рядом с другими `RelayCommand`-объявлениями добавить:

```csharp
SetMinRatingCommand = new RelayCommand(parameter =>
{
    if (!TryParseInt(parameter, out int requested)) return;
    // Toggle: клик по уже активной звезде → 0 («Все»).
    MinRating = MinRating == requested ? 0 : Math.Clamp(requested, 0, 5);
});
```

И объявление публичного свойства рядом с другими `public ICommand ... { get; }`:

```csharp
public ICommand SetMinRatingCommand { get; }
```

`TryParseInt` уже существует в файле (был добавлен в Task 6 итерации C для `SetRatingCommand`).

- [ ] **Step 5.2: Тест на SetMinRatingCommand**

Files: `MusicLibrary.Tests/MainViewModelTests.cs` — добавить рядом с тестами `SetRatingCommand_*`:

```csharp
[Fact]
public void SetMinRatingCommand_Toggle_Same_Value_Resets_To_Zero()
{
    var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
    var viewModel = CreateViewModelWithRepo(new RecordingTrackRepository(tracks));

    viewModel.SetMinRatingCommand.Execute("4");
    Assert.Equal(4, viewModel.MinRating);

    viewModel.SetMinRatingCommand.Execute("4");
    Assert.Equal(0, viewModel.MinRating);
}

[Fact]
public void SetMinRatingCommand_Clamps_Out_Of_Range_To_0_5()
{
    var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
    var viewModel = CreateViewModelWithRepo(new RecordingTrackRepository(tracks));

    viewModel.SetMinRatingCommand.Execute("9");
    Assert.Equal(5, viewModel.MinRating); // clamp вверх

    viewModel.SetMinRatingCommand.Execute("9"); // тот же → сбрасывает в 0 (toggle логика)
    Assert.Equal(0, viewModel.MinRating);
}
```

- [ ] **Step 5.3: Запуск VM-тестов**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "FullyQualifiedName~SetMinRatingCommand" --nologo`
Expected: 2 passed.

- [ ] **Step 5.4: Создать ZeroAsActiveBrushConverter**

Files: `MusicLibrary/Converters/ZeroAsActiveBrushConverter.cs`

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicLibrary.Converters;

/// <summary>
/// Возвращает ActiveBrush, когда value == 0 (или null). Используется в фильтр-popup
/// для пилюли «Все»: она активна, когда MinRating == 0.
/// </summary>
public sealed class ZeroAsActiveBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = Brushes.Gold;
    public Brush InactiveBrush { get; set; } = Brushes.Gray;

    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        int n = value is int i ? i : 0;
        return n == 0 ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
```

- [ ] **Step 5.5: Зарегистрировать конвертер в App.xaml**

Files: `MusicLibrary/App.xaml`

В блоке с другими конвертерами добавить:

```xml
<converters:ZeroAsActiveBrushConverter x:Key="ZeroAsActiveBrushConverter"
                                       ActiveBrush="{StaticResource PrimaryBrush}"
                                       InactiveBrush="{StaticResource MutedForegroundBrush}" />
```

- [ ] **Step 5.6: Добавить Popup ⋮ в MainWindow.xaml**

Files: `MusicLibrary/MainWindow.xaml` — внутри `<Grid Grid.Row="1">` (т.е. внутри toolbar `<Border>`), после кнопки ⋮:

```xml
<Popup x:Name="MoreFiltersPopup"
       PlacementTarget="{Binding ElementName=MoreFiltersButton}"
       Placement="Bottom"
       AllowsTransparency="True"
       StaysOpen="False"
       PopupAnimation="Fade">
    <Border Style="{StaticResource ToolbarPopupBorderStyle}" Width="280">
        <StackPanel>
            <TextBlock Text="Минимальный рейтинг"
                       Foreground="{StaticResource MutedForegroundBrush}"
                       FontSize="12"
                       Margin="0,0,0,6" />
            <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
                <Button Style="{StaticResource PopupPillButtonStyle}"
                        Content="Все"
                        Command="{Binding SetMinRatingCommand}"
                        CommandParameter="0"
                        Margin="0,0,8,0">
                    <Button.Foreground>
                        <Binding Path="MinRating" Converter="{StaticResource ZeroAsActiveBrushConverter}" />
                    </Button.Foreground>
                </Button>
                <Button Command="{Binding SetMinRatingCommand}" CommandParameter="1" Style="{StaticResource ToolbarIconButtonStyle}" Width="32" Height="32" Margin="0,0,2,0">
                    <TextBlock Text="★" FontSize="22" Foreground="{Binding MinRating, Converter={StaticResource RatingThresholdToBrushConverter}, ConverterParameter=1}" />
                </Button>
                <Button Command="{Binding SetMinRatingCommand}" CommandParameter="2" Style="{StaticResource ToolbarIconButtonStyle}" Width="32" Height="32" Margin="0,0,2,0">
                    <TextBlock Text="★" FontSize="22" Foreground="{Binding MinRating, Converter={StaticResource RatingThresholdToBrushConverter}, ConverterParameter=2}" />
                </Button>
                <Button Command="{Binding SetMinRatingCommand}" CommandParameter="3" Style="{StaticResource ToolbarIconButtonStyle}" Width="32" Height="32" Margin="0,0,2,0">
                    <TextBlock Text="★" FontSize="22" Foreground="{Binding MinRating, Converter={StaticResource RatingThresholdToBrushConverter}, ConverterParameter=3}" />
                </Button>
                <Button Command="{Binding SetMinRatingCommand}" CommandParameter="4" Style="{StaticResource ToolbarIconButtonStyle}" Width="32" Height="32" Margin="0,0,2,0">
                    <TextBlock Text="★" FontSize="22" Foreground="{Binding MinRating, Converter={StaticResource RatingThresholdToBrushConverter}, ConverterParameter=4}" />
                </Button>
                <Button Command="{Binding SetMinRatingCommand}" CommandParameter="5" Style="{StaticResource ToolbarIconButtonStyle}" Width="32" Height="32">
                    <TextBlock Text="★" FontSize="22" Foreground="{Binding MinRating, Converter={StaticResource RatingThresholdToBrushConverter}, ConverterParameter=5}" />
                </Button>
            </StackPanel>

            <TextBlock Text="Реакция"
                       Foreground="{StaticResource MutedForegroundBrush}"
                       FontSize="12"
                       Margin="0,0,0,6" />
            <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
                <Button Style="{StaticResource PopupPillButtonStyle}"
                        Content="Все"
                        Command="{Binding SetReactionFilterCommand}"
                        CommandParameter="Any"
                        Margin="0,0,8,0">
                    <Button.Foreground>
                        <Binding Path="ReactionFilter" Converter="{StaticResource ReactionMatchToBrushConverter}" ConverterParameter="Any" />
                    </Button.Foreground>
                </Button>
                <Button Command="{Binding SetReactionFilterCommand}" CommandParameter="Liked"
                        Style="{StaticResource ToolbarIconButtonStyle}" Width="40" Height="36" Margin="0,0,6,0">
                    <TextBlock Text="♥" FontSize="18"
                               Foreground="{Binding ReactionFilter, Converter={StaticResource LikeReactionToBrushConverter}, ConverterParameter=Liked}" />
                </Button>
                <Button Command="{Binding SetReactionFilterCommand}" CommandParameter="Disliked"
                        Style="{StaticResource ToolbarIconButtonStyle}" Width="40" Height="36">
                    <Grid Width="22" Height="22">
                        <TextBlock Text="♥" FontSize="18" HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Foreground="{Binding ReactionFilter, Converter={StaticResource DislikeReactionToBrushConverter}, ConverterParameter=Disliked}" />
                        <Line X1="2" Y1="20" X2="20" Y2="2" StrokeThickness="1.8" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                              Stroke="{Binding ReactionFilter, Converter={StaticResource DislikeReactionToBrushConverter}, ConverterParameter=Disliked}" />
                    </Grid>
                </Button>
            </StackPanel>

            <Button Style="{StaticResource PopupResetButtonStyle}"
                    Content="Сбросить все фильтры"
                    Command="{Binding ClearFiltersCommand}"
                    Click="OnResetFiltersClick" />
        </StackPanel>
    </Border>
</Popup>
```

`PlacementTarget` ссылается на `MoreFiltersButton` — переименуй кнопку ⋮ в `<Button x:Name="MoreFiltersButton" ...>` (раньше в Step 4.2 у неё не было имени).

- [ ] **Step 5.7: Обновить OnMoreFiltersClick и добавить OnResetFiltersClick в code-behind**

Files: `MusicLibrary/MainWindow.xaml.cs`

```csharp
private void OnMoreFiltersClick(object sender, RoutedEventArgs e)
{
    MoreFiltersPopup.IsOpen = !MoreFiltersPopup.IsOpen;
}

private void OnResetFiltersClick(object sender, RoutedEventArgs e)
{
    // ClearFiltersCommand уже отработал через Binding.Command; здесь только закрываем popup.
    MoreFiltersPopup.IsOpen = false;
}
```

- [ ] **Step 5.8: Сборка + полный тест-прогон**

Run:
```bash
dotnet build MusicLibrary/MusicLibrary.csproj --nologo
dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo
```
Expected: build clean, 205 passed (203 + 2 новых на `SetMinRatingCommand`).

- [ ] **Step 5.9: Smoke-запуск**

Run: `cd MusicLibrary && timeout 8 dotnet run --no-build --verbosity quiet 2>&1 | tail -3`
Expected: приложение работает; клик по ⋮ открывает popup с тремя секциями.

- [ ] **Step 5.10: Коммит**

```bash
git add MusicLibrary/ViewModels/MainViewModel.cs MusicLibrary/Converters/ZeroAsActiveBrushConverter.cs MusicLibrary/App.xaml MusicLibrary/MainWindow.xaml MusicLibrary/MainWindow.xaml.cs MusicLibrary.Tests/MainViewModelTests.cs
git commit -m "$(cat <<'EOF'
feat(ui): ⋮ popup with rating stars, reaction, reset

The toolbar's ⋮ button now opens a Popup with three sections:
* Minimum rating — "Все" pill + five clickable ★ buttons. Click N sets
  MinRating=N; click an already-lit star resets to 0 (same toggle UX
  as the selected-track rating row). Uses the existing
  RatingThresholdToBrushConverter; the "Все" pill is highlighted via a
  new ZeroAsActiveBrushConverter that turns it gold when MinRating=0.
* Reaction — "Все" / ♥ / ♥̶ buttons hooked to the existing
  SetReactionFilterCommand. Reuses existing reaction brush converters.
* Reset — full-width gold-bordered button bound to ClearFiltersCommand.

New SetMinRatingCommand on MainViewModel implements the toggle-to-zero
semantics. Two unit tests cover toggle and out-of-range clamp.

OnMoreFiltersClick toggles the popup open/closed; OnResetFiltersClick
closes the popup after the command fires.
EOF
)"
```

---

## Task 6 — Popup «+ N ещё ▾»: список скрытых тегов + обновление

Кнопка «+ N ещё ▾» открывает popup со списком тегов, скрытых `OverflowChipPanel`-ом. Внутри — те же чипы, но в WrapPanel (или вертикальном StackPanel). Клик переключает выделение, popup остаётся открытым (multi-select). Внизу popup-а — текстовая ссылка «Обновить список» (`RefreshTagFiltersCommand`).

**Files:**
- Modify: `MusicLibrary/MainWindow.xaml` (новый Popup)
- Modify: `MusicLibrary/MainWindow.xaml.cs` (handler `OnMoreTagsClick`)

- [ ] **Step 6.1: Добавить Popup «More tags» в MainWindow.xaml**

Внутри `<Border Grid.Row="1">` (toolbar), после `MoreFiltersPopup`:

```xml
<Popup x:Name="MoreTagsPopup"
       PlacementTarget="{Binding ElementName=TagChipsPanel}"
       Placement="Bottom"
       AllowsTransparency="True"
       StaysOpen="False"
       PopupAnimation="Fade">
    <Border Style="{StaticResource ToolbarPopupBorderStyle}" MinWidth="220" MaxWidth="320">
        <StackPanel>
            <TextBlock Text="Скрытые теги"
                       Foreground="{StaticResource MutedForegroundBrush}"
                       FontSize="12"
                       Margin="0,0,0,8" />
            <ItemsControl x:Name="HiddenTagsList">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <!-- ItemTemplate задаётся в code-behind, потому что DataContext каждой строки —
                     TagFilterItem, и мы используем тот же визуал чипа что в toolbar. -->
            </ItemsControl>
            <Button Style="{StaticResource PopupResetButtonStyle}"
                    Content="Обновить список"
                    Command="{Binding RefreshTagFiltersCommand}"
                    Margin="0,12,0,0" />
        </StackPanel>
    </Border>
</Popup>
```

- [ ] **Step 6.2: Обновить OnMoreTagsClick в code-behind**

Files: `MusicLibrary/MainWindow.xaml.cs`

```csharp
private void OnMoreTagsClick(object sender, RoutedEventArgs e)
{
    // Собираем список скрытых тегов = те TagFilterItem, чьи соответствующие
    // Border-чипы в OverflowChipPanel имеют Visibility=Collapsed.
    // Конвенция: TagChipsPanel.Children — это [chip_0, chip_1, ..., chip_{N-1}, more_pill].
    // Index в Children совпадает с индексом в VM.TagFilters (RebuildTagChips сохраняет порядок).

    var hidden = new System.Collections.Generic.List<MusicLibrary.ViewModels.TagFilterItem>();
    int childCount = TagChipsPanel.Children.Count;
    for (int i = 0; i < childCount - 1; i++) // -1: исключаем More-пилюлю
    {
        if (TagChipsPanel.Children[i].Visibility == Visibility.Collapsed
            && i < _viewModel.TagFilters.Count)
        {
            hidden.Add(_viewModel.TagFilters[i]);
        }
    }

    HiddenTagsList.ItemsSource = hidden;
    HiddenTagsList.ItemTemplate = (DataTemplate)FindResource("HiddenTagChipTemplate");
    MoreTagsPopup.IsOpen = true;
}
```

- [ ] **Step 6.3: Добавить HiddenTagChipTemplate в TrackTemplates.xaml**

Files: `MusicLibrary/Resources/TrackTemplates.xaml` — рядом с другими шаблонами:

```xml
<DataTemplate x:Key="HiddenTagChipTemplate">
    <Border Cursor="Hand" Margin="0,0,6,6">
        <Border.InputBindings>
            <MouseBinding MouseAction="LeftClick"
                          Command="{Binding DataContext.ToggleTagFilterCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                          CommandParameter="{Binding}" />
        </Border.InputBindings>
        <Border.Style>
            <Style TargetType="Border" BasedOn="{StaticResource TagChipStyle}">
                <Setter Property="Margin" Value="0,0,6,6" />
                <Style.Triggers>
                    <DataTrigger Binding="{Binding IsSelected}" Value="True">
                        <Setter Property="Background" Value="#33D4A574" />
                        <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}" />
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </Border.Style>
        <TextBlock Text="{Binding Tag.Name}" Style="{StaticResource TagChipTextStyle}" />
    </Border>
</DataTemplate>
```

- [ ] **Step 6.4: Сборка + smoke-запуск**

Run:
```bash
dotnet build MusicLibrary/MusicLibrary.csproj --nologo
cd MusicLibrary && timeout 8 dotnet run --no-build --verbosity quiet 2>&1 | tail -3
```
Expected: build clean, приложение работает.

- [ ] **Step 6.5: Тест-прогон**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 205 passed.

- [ ] **Step 6.6: Коммит**

```bash
git add MusicLibrary/MainWindow.xaml MusicLibrary/MainWindow.xaml.cs MusicLibrary/Resources/TrackTemplates.xaml
git commit -m "$(cat <<'EOF'
feat(ui): "+ N ещё ▾" popup lists hidden tag chips + refresh link

The More-pill in the tag chip row opens MoreTagsPopup attached to the
OverflowChipPanel. On click it walks the panel's children, picks the
ones with Visibility=Collapsed, maps them back to TagFilterItem by
index (RebuildTagChips preserves order), and feeds the result into a
WrapPanel with the HiddenTagChipTemplate.

Each hidden chip is the same Border/TextBlock visual as inline chips,
including the IsSelected DataTrigger that gold-tints the chip when the
filter is active. Clicking a chip fires ToggleTagFilterCommand on the
parent ViewModel without closing the popup — multi-select stays open
until the user clicks outside.

Below the chip list a "Обновить список" link runs the existing
RefreshTagFiltersCommand. Auto-refresh on tag changes in TagsWindow
remains deferred to 1.0.4.
EOF
)"
```

---

## Task 7 — Manual smoke + чистка warnings

Финальная проверка по чек-листу из спека.

- [ ] **Step 7.1: Прогнать полный тест-сьют**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category!=Benchmark" --nologo`
Expected: 205 passed (199 + 4 OverflowChipPanel + 2 SetMinRatingCommand).

- [ ] **Step 7.2: Бенчмарк FTS — убедиться, что новый toolbar не повлиял на путь поиска**

Run: `dotnet test MusicLibrary.Tests/MusicLibrary.Tests.csproj --filter "Category=Benchmark" --nologo`
Expected: бенчмарк зелёный, среднее < 100 мс.

- [ ] **Step 7.3: Запустить приложение и пройти чек-лист**

Run: `dotnet run --project MusicLibrary`

Проверить вручную:

- [ ] Дефолтный размер окна — видно ≥7 треков в списке.
- [ ] Поле поиска работает: ввод «queen» через 250 мс фильтрует список.
- [ ] `Ctrl+F` фокусирует поле поиска; `Esc` очищает и снимает фокус.
- [ ] Жанр ComboBox работает: смена жанра фильтрует список.
- [ ] Создать 8+ тегов в TagsWindow, привязать пару к одному треку. После закрытия Tags-окна нажать «Обновить список» в popup-е «+ N ещё ▾».
- [ ] При полном экране все 8 чипов видны в toolbar. При сужении окна часть уходит за «+ N ещё ▾» — клик открывает popup со скрытыми.
- [ ] Клик по чипу (видимому или в popup-е) переключает выделение, фильтр срабатывает.
- [ ] Клик ⋮ открывает popup рейтинг/реакция/Reset.
- [ ] Клик по ★N ставит MinRating=N, клик по горящей ★N сбрасывает в 0.
- [ ] Клик «Все» в секции рейтинга сбрасывает MinRating в 0.
- [ ] Клик ♥ устанавливает Liked-фильтр; повторный клик сбрасывает.
- [ ] Клик ♥̶ устанавливает Disliked; повторный клик сбрасывает.
- [ ] «Сбросить все фильтры» обнуляет поиск, жанр, рейтинг, реакцию, теги одним кликом; popup закрывается.
- [ ] При активной комбинации (поиск + жанр + теги + рейтинг + реакция) выдача — пересечение всех; порядок при активном поиске — по bm25.
- [ ] Левая колонка не содержит «зомби»-разметки от старой панели (визуально под списком треков — пусто).
- [ ] Окно `TagsWindow` (Ctrl+G) и `StatsWindow` (Ctrl+T) открываются как раньше — не повредили.

- [ ] **Step 7.4: Если в Step 7.3 что-то не работает — фикс инлайн, прогнать тесты, коммит**

Не описываю заранее — зависит от того, что вылезет. Если всё работает — Step 7.5.

- [ ] **Step 7.5: Финальный коммит с метками smoke**

Если правок не потребовалось, этот шаг — no-op. Если правки были, коммит:

```bash
git add -A
git commit -m "fix(ui): smoke fixes for filter toolbar redesign"
```

---

## Definition of Done

- [ ] Все Tasks 1-7 отмечены.
- [ ] 205 unit-тестов зелёные (199 на момент старта плана + 4 на OverflowChipPanel + 2 на SetMinRatingCommand).
- [ ] Бенчмарк FTS не просел.
- [ ] Ручной smoke по чек-листу Step 7.3 — все пункты ✓.
- [ ] Левая колонка содержит только список треков.
- [ ] При дефолтном окне видно ≥7 треков (было 4).
- [ ] Старые стили `SearchTextBoxStyle`, `GenreComboBoxStyle`, `TagChipStyle` переиспользованы, не задублированы.

## Сознательные ограничения этой задачи

- Авто-refresh тегов при изменениях в TagsWindow не делается — оставлена ручная кнопка «Обновить список». Event-based вариант — отдельная задача в 1.0.4+ (требует выбора между `IObservable`, `INotifyTagListChanged`-сервисом или WPF messenger).
- Pinned-теги (приоритетные) не добавляются — отвергнуты в брейншторминге.
- Horizontal scroll стрелками для тегов не делается.
- Адаптивная вёрстка под узкие окна (< 980 px) не оптимизируется.
- OverflowChipPanel не поддерживает многострочный wrap внутри toolbar — это ожидаемо: toolbar = одна строка по дизайну.

---

## Self-Review (после написания плана)

**Spec coverage:**
- ✅ Toolbar между header и колонками — Task 4.
- ✅ Поиск + Жанр в toolbar — Task 4.
- ✅ Тег-чипы с overflow «+ N ещё ▾» — Task 1 (Panel) + Task 4 (toolbar wiring) + Task 6 (popup).
- ✅ Popup ⋮ с рейтинг-звёздами + реакция + Reset — Task 5.
- ✅ Кликабельные звёзды с toggle-to-zero — Task 5 (SetMinRatingCommand).
- ✅ ZeroAsActiveBrushConverter для пилюли «Все» — Task 5.
- ✅ Левая колонка освобождается — Task 3 (удаление старой панели).
- ✅ Текстовая ссылка «Обновить список» внутри popup тегов — Task 6.
- ✅ Сознательно отложенные пункты (auto-refresh, pinned, scroll) — раздел «Сознательные ограничения».

**Placeholder scan:** есть один блок в Step 4.2 с «псевдо-разметкой» — он явно помечен как «заглушка» и тут же откорректирован Step 4.3 → Step 4.4. Текст плана прямо объясняет, что использовать. Это не TBD-placeholder, а описание тупикового подхода и его замены — оставляю.

**Type consistency:**
- `OverflowChipPanel.HiddenCount` — int, везде согласован.
- `SetMinRatingCommand` — ICommand принимает object параметр, парсится через `TryParseInt` (уже в файле).
- `MoreFiltersButton` — имя кнопки введено в Step 5.6, и используется в `PlacementTarget` там же.
- `TagChipsPanel` — `x:Name` введено в Step 4.2 и использовано в Step 4.4, Step 6.1, Step 6.2.

План внутренне непротиворечив. Готов к исполнению.
