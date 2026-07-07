using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Сохранение и загрузка настроек плеера (громкость, mute, режим повтора).
/// Заменяет IPlayerSettingsStorage из 1.0.0.
/// </summary>
public interface IPlayerSettingsRepository
{
    PlayerSettings Load();
    void Save(PlayerSettings settings);

    /// <summary>Сохранённый индекс активного режима левой колонки (см. MainViewMode). null — не сохранялось.</summary>
    int? LoadActiveViewIndex();

    /// <summary>Сохраняет активный режим левой колонки в KV-хранилище (ключ "active_view").</summary>
    void SaveActiveView(MainViewMode view);
}
