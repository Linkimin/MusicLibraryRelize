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
    public string AlbumKey => Artist + " " + Title;
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
