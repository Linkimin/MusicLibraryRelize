using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicBakh.Infrastructure.Persistence;

namespace MusicLibrary.Tests.TestSupport;

/// <summary>
/// Создаёт LibraryDbContext поверх in-memory SQLite. Соединение должно жить столько же,
/// сколько контекст — иначе SQLite освободит память. Тест отвечает за Dispose.
/// </summary>
internal sealed class InMemorySqliteDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LibraryDbContext> _options;

    public InMemorySqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new LibraryDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public LibraryDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
