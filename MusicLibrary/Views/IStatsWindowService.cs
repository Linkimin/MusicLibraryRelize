namespace MusicLibrary.Views;

/// <summary>
/// Открывает окно статистики поверх главного окна. Каждый вызов создаёт свежий
/// StatsViewModel (через DI), чтобы данные были актуальными.
/// </summary>
public interface IStatsWindowService
{
    void Show();
}
