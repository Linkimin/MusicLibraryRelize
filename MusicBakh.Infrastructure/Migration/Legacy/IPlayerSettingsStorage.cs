using MusicBakh.Core.Domain;
using System;

namespace MusicBakh.Infrastructure.Migration.Legacy;

/// <summary>
/// Чтение и запись пользовательских настроек плеера.
/// Load никогда не бросает исключений — при любой ошибке возвращает PlayerSettings.Default.
/// </summary>
[Obsolete("Используется только сервисом миграции JsonToSqliteMigrationService. Не использовать в новом коде.")]
public interface IPlayerSettingsStorage
{
    PlayerSettings Load();
    void Save(PlayerSettings settings);
}
