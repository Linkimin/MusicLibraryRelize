using System.Windows;
using System.Windows.Input;

namespace MusicLibrary.Views;

public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
        // Красит шапку и border окна в тёмный #16161F (как AddTrackWindow,
        // ConfirmationDialogWindow). Применяем после SourceInitialized — тогда
        // у окна уже есть HWND для DwmSetWindowAttribute.
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
