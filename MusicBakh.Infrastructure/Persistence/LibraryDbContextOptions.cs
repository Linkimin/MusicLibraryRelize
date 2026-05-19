namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Параметры подключения к локальной SQLite-базе.
/// Путь по умолчанию — %LocalAppData%\MusicLibrary\library.db.
/// </summary>
public sealed record LibraryDbContextOptions(string DatabasePath)
{
    public static LibraryDbContextOptions Default { get; } = new(
        DatabasePath: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicLibrary",
            "library.db"));

    public string ConnectionString => $"Data Source={DatabasePath}";
}
