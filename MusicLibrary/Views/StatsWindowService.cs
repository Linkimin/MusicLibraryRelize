using System.Windows;
using MusicLibrary.ViewModels;

namespace MusicLibrary.Views;

internal sealed class StatsWindowService : IStatsWindowService
{
    private readonly Func<StatsViewModel> _viewModelFactory;

    public StatsWindowService(Func<StatsViewModel> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public void Show()
    {
        var window = new StatsWindow
        {
            DataContext = _viewModelFactory(),
            Owner = Application.Current.MainWindow
        };
        window.Show();
    }
}
