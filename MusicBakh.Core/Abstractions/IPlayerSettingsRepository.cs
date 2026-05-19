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
}
