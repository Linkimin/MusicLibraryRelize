using System.IO;
using MusicBakh.Core.Domain;

namespace MusicBakh.Infrastructure.Seeding;

/// <summary>
/// Единый источник правды по встроенным трекам, которые поставляются вместе с приложением.
/// Файлы лежат в подкаталогах Music и Covers рядом с exe. Используется как BuiltInTrackSeeder
/// при первом запуске, так и InMemoryTrackRepository для модульных тестов.
/// </summary>
public static class BuiltInTracksProvider
{
    public static IReadOnlyList<Track> GetDefaults()
    {
        string musicFolder = Path.Combine(AppContext.BaseDirectory, "Music");
        string coversFolder = Path.Combine(AppContext.BaseDirectory, "Covers");

        return new List<Track>
        {
            Create(1, "Я свободен", "Кипелов", "Рок", 204, "Кипелов - Я свободен.mp3", "ya-svoboden.jpg"),
            Create(2, "Hayloft II", "Mother Mother", "Инди", 215, "Mother Mother - Hayloft II.mp3", "hayloft-ii.jpg"),
            Create(3, "VORACITY", "MYTH ROID", "Аниме/OST", 230, "MYTH ROID - VORACITY (ПовелительВладыка ТВ-3Overlord TV-3 OP).mp3", "voracity.jpg")
        };

        Track Create(int id, string title, string artist, string genre, int durationSeconds, string fileName, string coverName) =>
            new()
            {
                Id = id,
                Title = title,
                Artist = artist,
                Genre = genre,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FilePath = Path.Combine(musicFolder, fileName),
                CoverPath = Path.Combine(coversFolder, coverName),
                IsBuiltIn = true
            };
    }
}
