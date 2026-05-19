using System.Collections.ObjectModel;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;

namespace MusicLibrary.ViewModels;

/// <summary>
/// ViewModel экрана статистики прослушиваний. Загружает три коллекции при создании;
/// данные на локальной SQLite размера типичной библиотеки укладываются в десятки мс,
/// async только усложнил бы биндинг без выгоды. Новый instance создаётся каждый раз
/// при открытии окна (transient в DI), поэтому актуальность гарантирована.
/// </summary>
public sealed class StatsViewModel : ViewModelBase
{
    public StatsViewModel(IListeningHistoryRepository history)
    {
        Top         = new ObservableCollection<ListeningStats>(history.GetTop(50));
        Recent      = new ObservableCollection<PlaybackEntry>(history.GetRecentUnique(50));
        NeverPlayed = new ObservableCollection<Track>(history.GetNeverPlayed());
    }

    public ObservableCollection<ListeningStats> Top { get; }
    public ObservableCollection<PlaybackEntry>  Recent { get; }
    public ObservableCollection<Track>          NeverPlayed { get; }

    public bool HasTop         => Top.Count > 0;
    public bool HasRecent      => Recent.Count > 0;
    public bool HasNeverPlayed => NeverPlayed.Count > 0;
}
