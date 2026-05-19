#pragma warning disable CS0618 // Сервис мигрирует данные из legacy-хранилища — единственное санкционированное использование Obsolete-типов.

using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Migration.Legacy;

namespace MusicBakh.Infrastructure.Migration;

/// <summary>
/// Результат однократного запуска миграции. PerformedMigration=true означает, что
/// файл userTracks.json существовал в начале запуска (даже если записей в нём не оказалось).
/// </summary>
public sealed record MigrationResult(bool PerformedMigration, int MigratedTracks, string? BackupPath);

/// <summary>
/// Одноразовая миграция userTracks.json → SQLite при первом запуске после апгрейда с 1.0.x.
/// После переноса записей файл переименовывается в userTracks.json.backup-&lt;timestamp&gt;,
/// чтобы пользователь мог откатиться вручную при необходимости.
/// </summary>
public sealed class JsonToSqliteMigrationService
{
    private readonly string _rootDirectory;
    private readonly ITrackRepository _trackRepository;

    public JsonToSqliteMigrationService(string rootDirectory, ITrackRepository trackRepository)
    {
        _rootDirectory = rootDirectory;
        _trackRepository = trackRepository;
    }

    public MigrationResult Run()
    {
        var legacyPath = Path.Combine(_rootDirectory, "userTracks.json");
        if (!File.Exists(legacyPath))
        {
            return new MigrationResult(PerformedMigration: false, MigratedTracks: 0, BackupPath: null);
        }

        var legacy = new JsonUserTrackStorage(_rootDirectory);
        var tracks = legacy.Load();

        int migrated = 0;
        foreach (var t in tracks)
        {
            _trackRepository.Add(new Track
            {
                Title = t.Title,
                Artist = t.Artist,
                Genre = t.Genre,
                Duration = TimeSpan.FromSeconds(t.DurationSeconds),
                FilePath = t.FilePath,
                CoverPath = t.CoverPath
            });
            migrated++;
        }

        var backupPath = Path.Combine(
            _rootDirectory,
            $"userTracks.json.backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        File.Move(legacyPath, backupPath);

        return new MigrationResult(PerformedMigration: true, MigratedTracks: migrated, BackupPath: backupPath);
    }
}

#pragma warning restore CS0618
