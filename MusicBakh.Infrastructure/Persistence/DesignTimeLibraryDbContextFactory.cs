using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Фабрика контекста для команд `dotnet ef migrations`. Использует временный путь к БД;
/// настоящий путь подставляется в рантайме через DI.
/// </summary>
internal sealed class DesignTimeLibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new LibraryDbContext(options);
    }
}
