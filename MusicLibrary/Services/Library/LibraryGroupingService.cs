using System;
using System.Collections.Generic;
using System.Linq;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Pure-функции группировки треков в альбомы и исполнителей.
/// Ключ альбома: (AlbumArtist ?? Artist, Album).
/// Ключ исполнителя: Track.Artist (НЕ AlbumArtist) — в compilations
/// каждый исполнитель виден сам по себе.
/// </summary>
public static class LibraryGroupingService
{
    public static IReadOnlyList<AlbumAggregate> GroupByAlbum(IReadOnlyList<Track> tracks)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return Array.Empty<AlbumAggregate>();
        }

        var groups = tracks
            .GroupBy(t => (
                Artist: string.IsNullOrWhiteSpace(t.AlbumArtist) ? t.Artist : t.AlbumArtist!,
                Title: t.Album ?? string.Empty));

        var aggregates = new List<AlbumAggregate>();
        foreach (var g in groups)
        {
            var sortedTracks = g
                .OrderBy(t => t.TrackNumber.HasValue ? 0 : 1)
                .ThenBy(t => t.TrackNumber ?? int.MaxValue)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var year = g
                .Where(t => t.Year.HasValue)
                .Select(t => (int?)t.Year!.Value)
                .DefaultIfEmpty(null)
                .Max();

            var firstByid = g.OrderBy(t => t.Id).First();

            aggregates.Add(new AlbumAggregate(
                Title: g.Key.Title,
                Artist: g.Key.Artist,
                Year: year,
                CoverPath: firstByid.CoverPath ?? string.Empty,
                Tracks: sortedTracks));
        }

        // Сортировка альбомов: Year DESC NULLS LAST, Title ASC.
        return aggregates
            .OrderBy(a => a.Year.HasValue ? 0 : 1)
            .ThenByDescending(a => a.Year ?? 0)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ArtistAggregate> GroupByArtist(IReadOnlyList<Track> tracks)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return Array.Empty<ArtistAggregate>();
        }

        var byArtist = tracks
            .GroupBy(t => t.Artist ?? string.Empty);

        var aggregates = new List<ArtistAggregate>();
        foreach (var ag in byArtist)
        {
            // Треки этого артиста — отдельно те, что в альбомах, и те, что «прочие» (Album пустой).
            var withAlbum = ag.Where(t => !string.IsNullOrWhiteSpace(t.Album)).ToList();
            var loose = ag
                .Where(t => string.IsNullOrWhiteSpace(t.Album))
                .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var albums = GroupByAlbum(withAlbum);

            var totalDuration = TimeSpan.Zero;
            foreach (var t in ag)
            {
                totalDuration += t.Duration;
            }

            aggregates.Add(new ArtistAggregate(
                Name: ag.Key,
                Albums: albums,
                LooseTracks: loose,
                TotalTracks: ag.Count(),
                TotalDuration: totalDuration));
        }

        // Сортировка исполнителей: TotalTracks DESC, Name ASC.
        return aggregates
            .OrderByDescending(a => a.TotalTracks)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
