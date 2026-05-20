using System.Windows;
using System.Windows.Input;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;

namespace MusicLibrary;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private System.Collections.Specialized.INotifyCollectionChanged? _tagFiltersSource;
    private System.Windows.Controls.Button? _morePillCached;

    public MainWindow(MainViewModel viewModel, IPlayerSettingsRepository playerSettingsRepository)
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;

        _viewModel = viewModel;

        // Гидратируем ViewModel сохранёнными настройками плеера (громкость, mute, режим повтора).
        var settings = playerSettingsRepository.Load();
        _viewModel.Volume = settings.Volume;
        _viewModel.IsMuted = settings.IsMuted;
        _viewModel.RepeatMode = settings.RepeatMode;

        DataContext = _viewModel;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowAppearance.Apply(this);
    }

    private void OnFindCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        // Ctrl+F: фокус и выделение текста в строке поиска. Это чисто UI-операция
        // над конкретным TextBox, поэтому живёт в code-behind, а не в команде VM.
        SearchBox.Focus();
        SearchBox.SelectAll();
        e.Handled = true;
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Эскейп — очистить и снять фокус. ViewModel получит пустой SearchText
            // через биндинг и вернётся к полной библиотеке.
            SearchBox.Clear();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        HookTagFiltersSync();
        RebuildTagChips();
    }

    private void HookTagFiltersSync()
    {
        if (_viewModel.TagFilters is System.Collections.Specialized.INotifyCollectionChanged ncc && _tagFiltersSource is null)
        {
            ncc.CollectionChanged += (_, _) => RebuildTagChips();
            _tagFiltersSource = ncc;
        }
    }

    private void RebuildTagChips()
    {
        System.Windows.Controls.Button morePill = EnsureMorePill();
        TagChipsPanel.Children.Clear();

        foreach (var item in _viewModel.TagFilters)
        {
            TagChipsPanel.Children.Add(CreateChip(item));
        }
        TagChipsPanel.Children.Add(morePill);
    }

    private System.Windows.Controls.Button EnsureMorePill()
    {
        if (_morePillCached is null)
        {
            var tb = new System.Windows.Controls.TextBlock();
            tb.Inlines.Add(new System.Windows.Documents.Run("+ "));
            var countRun = new System.Windows.Documents.Run();
            System.Windows.Data.BindingOperations.SetBinding(countRun,
                System.Windows.Documents.Run.TextProperty,
                new System.Windows.Data.Binding(nameof(MusicLibrary.Controls.OverflowChipPanel.HiddenCount))
                {
                    Source = TagChipsPanel,
                    Mode = System.Windows.Data.BindingMode.OneWay
                });
            tb.Inlines.Add(countRun);
            tb.Inlines.Add(new System.Windows.Documents.Run(" ещё ▾"));

            _morePillCached = new System.Windows.Controls.Button
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
            Cursor = System.Windows.Input.Cursors.Hand,
            DataContext = item,
            Margin = new Thickness(0, 0, 6, 0)
        };

        // Стиль чипа с IsSelected-триггером для подсветки активного.
        var styleOverride = new Style(typeof(System.Windows.Controls.Border),
            (Style)FindResource("TagChipStyle"));
        var trigger = new System.Windows.DataTrigger
        {
            Binding = new System.Windows.Data.Binding(nameof(MusicLibrary.ViewModels.TagFilterItem.IsSelected)),
            Value = true
        };
        trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xD4, 0xA5, 0x74))));
        trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BorderBrushProperty,
            (System.Windows.Media.Brush)FindResource("PrimaryBrush")));
        styleOverride.Triggers.Add(trigger);
        chipBorder.Style = styleOverride;

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
        // Task 6 fills in the popup logic.
    }

    private void OnMoreFiltersClick(object sender, RoutedEventArgs e)
    {
        // Task 5 fills in the popup logic.
    }

    private void OnSeekDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _viewModel.IsSeeking = true;
    }

    private void OnSeekPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            _viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value));
        }

        _viewModel.IsSeeking = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
