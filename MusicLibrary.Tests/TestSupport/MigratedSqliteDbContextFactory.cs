using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicBakh.Infrastructure.Persistence;

namespace MusicLibrary.Tests.TestSupport;

/// <summary>
/// Создаёт LibraryDbContext поверх in-memory SQLite и накатывает реальные EF Core
/// миграции через Database.Migrate(). Это отличается от InMemorySqliteDbContextFactory,
/// который зовёт EnsureCreated() и пропускает raw-SQL миграции (FTS5 виртуальная
/// таблица, триггеры). Использовать для тестов, которые зависят от FTS или другого
/// schema-инвариант, заведённого migrationBuilder.Sql.
///
/// ВАЖНО: фабрика держит один общий SqliteConnection и переиспользует его для всех
/// контекстов. Это работает только потому, что in-memory SQLite живёт ровно столько,
/// сколько открыто соединение. Не делитесь экземпляром фабрики между потоками и не
/// переходите на AddDbContextFactory с реальным connection pool — миграции и
/// контексты обязаны видеть одну и ту же in-memory БД.
/// </summary>
internal sealed class MigratedSqliteDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LibraryDbContext> _options;

    public MigratedSqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new LibraryDbContext(_options);
        ctx.Database.Migrate();
    }

    public LibraryDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
