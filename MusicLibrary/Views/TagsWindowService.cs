using System.Windows;
using MusicLibrary.ViewModels;

namespace MusicLibrary.Views;

internal sealed class TagsWindowService : ITagsWindowService
{
    private readonly Func<TagsViewModel> _viewModelFactory;

    public TagsWindowService(Func<TagsViewModel> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public void Show()
    {
        var window = new TagsWindow
        {
            DataContext = _viewModelFactory(),
            Owner = Application.Current.MainWindow
        };
        window.Show();
    }
}
