namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>
/// KV-стор для разнородных настроек: громкость, mute, режим повтора, в будущем — пресеты эквалайзера и т.п.
/// Гибкая схема: новые ключи можно вводить без миграции.
/// </summary>
internal sealed class KeyValueEntryEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
