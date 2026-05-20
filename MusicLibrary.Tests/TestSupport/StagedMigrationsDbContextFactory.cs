using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MusicBakh.Infrastructure.Persistence;

namespace MusicLibrary.Tests.TestSupport;

/// <summary>
/// Создаёт LibraryDbContext поверх in-memory SQLite и позволяет накатывать миграции
/// поэтапно через IMigrator.Migrate(targetMigration). Нужна для тестов, которые
/// проверяют переход между версиями схемы — например, что миграция AddTracksFts
/// корректно бэкфилит уже существующие в Tracks строки в FTS-индекс.
///
/// В отличие от <see cref="MigratedSqliteDbContextFactory"/>, который сразу
/// накатывает все миграции до головы, эта фабрика стартует с пустой БД и ожидает,
/// что тест вызовет <see cref="MigrateUpTo"/> один или несколько раз. Фазы апгрейда
/// в тесте читаются явно — это и есть смысл фикстуры.
///
/// ВАЖНО: как и MigratedSqliteDbContextFactory, держит один общий SqliteConnection
/// на всё время жизни — иначе in-memory БД пропадёт между шагами. Не делитесь
/// экземпляром между потоками.
/// </summary>
internal sealed class StagedMigrationsDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LibraryDbContext> _options;

    public StagedMigrationsDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public LibraryDbContext CreateContext() => new(_options);

    /// <summary>
    /// Накатывает миграции до указанной включительно. Имя — без расширения и без
    /// пути, в той форме, в которой EF Core хранит миграции в __EFMigrationsHistory
    /// (например, "20260519183130_AddTrackAlbum"). Можно вызывать многократно,
    /// чтобы симулировать поэтапный апгрейд схемы.
    /// </summary>
    public void MigrateUpTo(string targetMigration)
    {
        using var ctx = new LibraryDbContext(_options);
        var migrator = ctx.GetInfrastructure().GetRequiredService<IMigrator>();
        migrator.Migrate(targetMigration);
    }

    public void Dispose() => _connection.Dispose();
}
