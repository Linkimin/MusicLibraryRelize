using System.Windows;
using System.Windows.Input;

namespace MusicLibrary.Views;

public partial class TagsWindow : Window
{
    public TagsWindow()
    {
        InitializeComponent();
        // Тёмная шапка через DWM — единый паттерн с AddTrackWindow / StatsWindow.
        SourceInitialized += (_, _) => NativeWindowAppearance.Apply(this);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
