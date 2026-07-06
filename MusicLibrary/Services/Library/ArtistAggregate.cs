using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Агрегат исполнителя (computed). Identity = Name.
/// </summary>
public sealed record ArtistAggregate(
    string Name,
    System.Collections.Generic.IReadOnlyList<AlbumAggregate> Albums,
    System.Collections.Generic.IReadOnlyList<Track> LooseTracks,
    int TotalTracks,
    System.TimeSpan TotalDuration);
