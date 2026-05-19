using System.Windows;
using System.Windows.Input;

namespace MusicLibrary.Views;

public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
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
