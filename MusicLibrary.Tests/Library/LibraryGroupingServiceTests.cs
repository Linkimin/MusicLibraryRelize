using System.Linq;
using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using Xunit;

namespace MusicLibrary.Tests.Library;

public sealed class LibraryGroupingServiceTests
{
    private static Track T(int id, string title, string artist, string album, int? year = null, int? trackNo = null, string? albumArtist = null, int durationSec = 180, string coverPath = "")
        => new()
        {
            Id = id,
            Title = title,
            Artist = artist,
            Album = album,
            Year = year,
            TrackNumber = trackNo,
            AlbumArtist = albumArtist,
            Duration = System.TimeSpan.FromSeconds(durationSec),
            FilePath = id + ".mp3",
            CoverPath = coverPath
        };

    // === GroupByAlbum ===

    [Fact]
    public void GroupByAlbum_Empty_Input_Returns_Empty_List()
    {
        var result = LibraryGroupingService.GroupByAlbum(System.Array.Empty<Track>());
        Assert.Empty(result);
    }

    [Fact]
    public void GroupByAlbum_Groups_By_AlbumArtist_When_Present_Else_By_Artist()
    {
        var tracks = new[]
        {
            T(1, "A", "Bowie",   "Hunky Dory", albumArtist: "David Bowie"),
            T(2, "B", "Bowie",   "Hunky Dory", albumArtist: "David Bowie"),
            T(3, "C", "Queen",   "The Works" /* AlbumArtist=null */),
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Artist == "David Bowie" && a.Title == "Hunky Dory" && a.Tracks.Count == 2);
        Assert.Contains(result, a => a.Artist == "Queen" && a.Title == "The Works" && a.Tracks.Count == 1);
    }

    [Fact]
    public void GroupByAlbum_Compilations_Merge_Under_Various_Artists()
    {
        var tracks = new[]
        {
            T(1, "X", "Queen",      "Greatest Hits 1990", albumArtist: "Various Artists"),
            T(2, "Y", "Beatles",    "Greatest Hits 1990", albumArtist: "Various Artists"),
            T(3, "Z", "Pink Floyd", "Greatest Hits 1990", albumArtist: "Various Artists"),
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);

        var album = Assert.Single(result);
        Assert.Equal("Various Artists", album.Artist);
        Assert.Equal("Greatest Hits 1990", album.Title);
        Assert.Equal(3, album.Tracks.Count);
    }

    [Fact]
    public void GroupByAlbum_Sorts_Tracks_By_TrackNumber_NullsLast_Then_Title()
    {
        var tracks = new[]
        {
            T(1, "Beta",  "A", "X", trackNo: 2),
            T(2, "Alpha", "A", "X", trackNo: 1),
            T(3, "Zeta",  "A", "X" /* TrackNumber=null */),
            T(4, "Gamma", "A", "X" /* TrackNumber=null */),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();

        // 1 (Alpha, TN=1), 2 (Beta, TN=2), then nulls by title: Gamma, Zeta.
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma", "Zeta" }, album.Tracks.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void GroupByAlbum_Year_Equals_Max_Of_Track_Years_NullsIgnored()
    {
        var tracks = new[]
        {
            T(1, "A", "X", "Y", year: 2010),
            T(2, "B", "X", "Y", year: 2012),
            T(3, "C", "X", "Y", year: null),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();
        Assert.Equal(2012, album.Year);
    }

    [Fact]
    public void GroupByAlbum_CoverPath_Equals_First_Track_By_Id()
    {
        var tracks = new[]
        {
            T(7, "A", "X", "Y", coverPath: "cover7.jpg"),
            T(3, "B", "X", "Y", coverPath: "cover3.jpg"),
            T(5, "C", "X", "Y", coverPath: "cover5.jpg"),
        };

        var album = LibraryGroupingService.GroupByAlbum(tracks).Single();
        Assert.Equal("cover3.jpg", album.CoverPath);
    }

    [Fact]
    public void GroupByAlbum_Sorts_Albums_By_Year_Desc_NullsLast_Then_Title_Asc()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "Album Z", year: 2020),
            T(2, "b", "X", "Album A", year: 2020),  // same year, title asc → Album A first
            T(3, "c", "X", "Album M", year: 2022),  // newest
            T(4, "d", "X", "Album N" /* year=null */), // unknown year → last
        };

        var result = LibraryGroupingService.GroupByAlbum(tracks);
        Assert.Equal(new[] { "Album M", "Album A", "Album Z", "Album N" }, result.Select(a => a.Title).ToArray());
    }

    // === GroupByArtist ===

    [Fact]
    public void GroupByArtist_Groups_By_Track_Artist_Not_AlbumArtist()
    {
        // Compilation: трек Queen в альбоме «Various Artists» — должен попасть к Queen.
        var tracks = new[]
        {
            T(1, "A", "Queen",   "Comp 1990", albumArtist: "Various Artists"),
            T(2, "B", "Queen",   "Queen Hits"),
            T(3, "C", "Beatles", "Comp 1990", albumArtist: "Various Artists"),
        };

        var result = LibraryGroupingService.GroupByArtist(tracks);

        Assert.Equal(2, result.Count);
        var queen = Assert.Single(result, a => a.Name == "Queen");
        Assert.Equal(2, queen.TotalTracks);
        var beatles = Assert.Single(result, a => a.Name == "Beatles");
        Assert.Equal(1, beatles.TotalTracks);
    }

    [Fact]
    public void GroupByArtist_LooseTracks_Contains_Only_Empty_Album_Tracks()
    {
        var tracks = new[]
        {
            T(1, "Loose1",  "X", ""),
            T(2, "InAlb",   "X", "Some Album"),
            T(3, "Loose2",  "X", ""),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();

        Assert.Equal(2, artist.LooseTracks.Count);
        Assert.Equal(new[] { "Loose1", "Loose2" }, artist.LooseTracks.Select(t => t.Title).ToArray());
        Assert.Single(artist.Albums);
        Assert.Equal("Some Album", artist.Albums[0].Title);
    }

    [Fact]
    public void GroupByArtist_Sorts_Artists_By_TrackCount_Desc_Then_Name_Asc()
    {
        var tracks = new[]
        {
            T(1, "a", "Bob",   "X"),
            T(2, "b", "Alice", "X"),
            T(3, "c", "Alice", "Y"),
            T(4, "d", "Carl",  "X"),
            T(5, "e", "Carl",  "Y"),
        };

        var result = LibraryGroupingService.GroupByArtist(tracks);

        // Alice=2, Carl=2 — count tie → name asc (Alice, Carl). Bob=1 last.
        Assert.Equal(new[] { "Alice", "Carl", "Bob" }, result.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void GroupByArtist_TotalDuration_Sums_All_Tracks()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "A", durationSec: 100),
            T(2, "b", "X", "A", durationSec: 200),
            T(3, "c", "X", "",  durationSec: 50),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();
        Assert.Equal(System.TimeSpan.FromSeconds(350), artist.TotalDuration);
    }

    [Fact]
    public void GroupByArtist_Sorts_Albums_By_Year_Desc_NullsLast()
    {
        var tracks = new[]
        {
            T(1, "a", "X", "Old",     year: 2010),
            T(2, "b", "X", "New",     year: 2024),
            T(3, "c", "X", "Unknown" /* year=null */),
        };

        var artist = LibraryGroupingService.GroupByArtist(tracks).Single();

        Assert.Equal(new[] { "New", "Old", "Unknown" }, artist.Albums.Select(a => a.Title).ToArray());
    }

    // === AlbumKey / loose-track семантика (согласование со спекой) ===

    [Fact]
    public void AlbumKey_Does_Not_Collide_On_Ambiguous_Artist_Title_Concatenation()
    {
        // Без разделителя "AB"+"C" == "A"+"BC". Разделитель U+001F исключает коллизию.
        var albums = LibraryGroupingService.GroupByAlbum(new[]
        {
            T(1, "t1", "AB", "C"),
            T(2, "t2", "A", "BC"),
        });

        var keys = albums.Select(a => a.AlbumKey).ToArray();
        Assert.Equal(2, keys.Length);
        Assert.NotEqual(keys[0], keys[1]);
    }

    [Fact]
    public void GroupByArtist_Whitespace_Album_Is_Not_Loose()
    {
        // Loose = IsNullOrEmpty: пробельный Album — это альбом с пустым названием, не «прочий».
        var artist = LibraryGroupingService.GroupByArtist(new[]
        {
            T(1, "t1", "X", " "),
        }).Single();

        Assert.Empty(artist.LooseTracks);
        Assert.Single(artist.Albums);
    }

    [Fact]
    public void GroupByAlbum_Excludes_Tracks_With_Empty_Album()
    {
        // Треки без тега Album не должны порождать фантомные альбомы с пустым названием.
        var albums = LibraryGroupingService.GroupByAlbum(new[]
        {
            T(1, "t1", "Muse", ""),                 // без альбома → не альбом
            T(2, "t2", "Sabaton", "Heroes"),        // с альбомом → альбом
            T(3, "t3", "Twenty One Pilots", null!), // null альбом → не альбом
        });

        Assert.Single(albums);
        Assert.Equal("Heroes", albums[0].Title);
    }

    [Fact]
    public void GroupByAlbum_All_Empty_Albums_Returns_Empty()
    {
        var albums = LibraryGroupingService.GroupByAlbum(new[]
        {
            T(1, "t1", "Muse", ""),
            T(2, "t2", "Sabaton", ""),
        });

        Assert.Empty(albums);
    }
}
