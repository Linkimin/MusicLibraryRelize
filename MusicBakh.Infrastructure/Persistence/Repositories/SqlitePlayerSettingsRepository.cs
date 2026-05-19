using System.Globalization;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// Настройки плеера хранятся в KV-таблице: три ключа — громкость, mute, режим повтора.
/// Такая модель позволит в будущем добавлять новые настройки без миграций схемы.
/// </summary>
public sealed class SqlitePlayerSettingsRepository : IPlayerSettingsRepository
{
    private const string VolumeKey = "player.volume";
    private const string IsMutedKey = "player.isMuted";
    private const string RepeatModeKey = "player.repeatMode";

    private readonly Func<LibraryDbContext> _contextFactory;

    public SqlitePlayerSettingsRepository(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public PlayerSettings Load()
    {
        using var ctx = _contextFactory();
        var entries = ctx.KeyValueStore.ToDictionary(k => k.Key, k => k.Value);

        return new PlayerSettings(
            Volume: ReadDouble(entries, VolumeKey, PlayerSettings.Default.Volume),
            IsMuted: ReadBool(entries, IsMutedKey, PlayerSettings.Default.IsMuted),
            RepeatMode: ReadEnum(entries, RepeatModeKey, PlayerSettings.Default.RepeatMode));
    }

    public void Save(PlayerSettings settings)
    {
        using var ctx = _contextFactory();
        Upsert(ctx, VolumeKey, settings.Volume.ToString(CultureInfo.InvariantCulture));
        Upsert(ctx, IsMutedKey, settings.IsMuted ? "true" : "false");
        Upsert(ctx, RepeatModeKey, settings.RepeatMode.ToString());
        ctx.SaveChanges();
    }

    private static void Upsert(LibraryDbContext ctx, string key, string value)
    {
        var existing = ctx.KeyValueStore.FirstOrDefault(k => k.Key == key);
        if (existing is null)
        {
            ctx.KeyValueStore.Add(new KeyValueEntryEntity { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
    }

    private static double ReadDouble(IDictionary<string, string> map, string key, double fallback) =>
        map.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    private static bool ReadBool(IDictionary<string, string> map, string key, bool fallback) =>
        map.TryGetValue(key, out var raw) && bool.TryParse(raw, out var v) ? v : fallback;

    private static RepeatMode ReadEnum(IDictionary<string, string> map, string key, RepeatMode fallback) =>
        map.TryGetValue(key, out var raw) && Enum.TryParse<RepeatMode>(raw, out var v) ? v : fallback;
}
