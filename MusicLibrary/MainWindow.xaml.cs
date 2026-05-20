using System.Windows;
using System.Windows.Input;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;

namespace MusicLibrary;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

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
        // TODO Task 4: Restore after toolbar lands
        // SearchBox.Focus();
        // SearchBox.SelectAll();
        e.Handled = true;
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Эскейп — очистить и снять фокус. ViewModel получит пустой SearchText
            // через биндинг и вернётся к полной библиотеке.
            // TODO Task 4: Restore after toolbar lands
            // SearchBox.Clear();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
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
