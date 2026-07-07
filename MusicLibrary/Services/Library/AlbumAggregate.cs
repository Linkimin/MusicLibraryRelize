using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Агрегат альбома (computed из треков, не persisted). Identity = (Artist, Title).
/// </summary>
public sealed record AlbumAggregate(
    string Title,
    string Artist,
    int? Year,
    string CoverPath,
    System.Collections.Generic.IReadOnlyList<Track> Tracks)
{
    // Разделитель U+001F (Unit Separator) не встречается в тегах ID3, поэтому
    // ключ (Artist, Title) не коллизирует (например, "AB"+"C" != "A"+"BC").
    public string AlbumKey => Artist + ((char)0x1F) + Title;
    public System.TimeSpan TotalDuration
    {
        get
        {
            var sum = System.TimeSpan.Zero;
            foreach (var t in Tracks) sum += t.Duration;
            return sum;
        }
    }
}
