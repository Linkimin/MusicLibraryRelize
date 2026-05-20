using MusicBakh.Core.Domain;
using MusicLibrary.Services.Library;
using Xunit;

namespace MusicLibrary.Tests.Library;

public sealed class LibraryFilterTests
{
    private static readonly Track[] Sample =
    {
        new() { Id = 1, Title = "Rock A", Artist = "Band", Genre = "Рок",  Rating = 5, Reaction = TrackReaction.Liked },
        new() { Id = 2, Title = "Rock B", Artist = "Band", Genre = "Рок",  Rating = 3, Reaction = TrackReaction.None },
        new() { Id = 3, Title = "Jazz A", Artist = "Quartet", Genre = "Джаз", Rating = 4, Reaction = TrackReaction.Disliked },
        new() { Id = 4, Title = "Pop X",  Artist = "Solo", Genre = "Поп",  Rating = 0, Reaction = TrackReaction.None }
    };

    private static readonly Dictionary<int, IReadOnlyList<int>> EmptyTags = new();

    [Fact]
    public void Empty_Criteria_Returns_All_Tracks_In_Original_Order()
    {
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 0, null, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Empty_Library_Returns_Empty_Without_Lookups()
    {
        var result = LibraryFilter.Apply(
            Array.Empty<Track>(),
            new LibraryFilterCriteria(null, "Рок", 5, TrackReaction.Liked, new[] { 1 }),
            tagIdsOfTrack: null);

        Assert.Empty(result);
    }

    [Fact]
    public void SearchHits_Preserves_Order_Even_When_AllTracks_Order_Differs()
    {
        // SearchHits: id 3, 1, 2 — ровно тот порядок, что отдаёт FTS по bm25.
        var hits = new[] { Sample[2], Sample[0], Sample[1] };
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(hits, null, 0, null, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(new[] { 3, 1, 2 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void SearchHits_Combined_With_Genre_Intersects_And_Keeps_Search_Order()
    {
        var hits = new[] { Sample[2], Sample[1], Sample[0] }; // Jazz A, Rock B, Rock A
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(hits, "Рок", 0, null, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(new[] { 2, 1 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void MinRating_Keeps_Only_Tracks_At_Or_Above_Threshold()
    {
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 4, null, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(new[] { 1, 3 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void MinRating_Zero_Is_No_Op()
    {
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 0, null, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Reaction_Liked_Filters_Out_Others()
    {
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 0, TrackReaction.Liked, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Equal(new[] { 1 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void TagIds_OR_Semantics_Returns_Tracks_With_Any_Of_Listed_Tags()
    {
        // Сопоставление trackId → tagIds:
        // 1: [10],  2: [20],  3: [10, 30],  4: []
        var assoc = new Dictionary<int, IReadOnlyList<int>>
        {
            { 1, new[] { 10 } },
            { 2, new[] { 20 } },
            { 3, new[] { 10, 30 } },
            { 4, Array.Empty<int>() }
        };

        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 0, null, new[] { 10, 99 }),
            id => assoc.TryGetValue(id, out var list) ? list : Array.Empty<int>());

        Assert.Equal(new[] { 1, 3 }, result.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void All_Criteria_Combined_AND_Across_Categories()
    {
        // Поиск даёт [2, 1, 3] → жанр Рок оставит [2, 1] → MinRating=4 оставит [1] →
        // Reaction=Liked оставит [1].
        var hits = new[] { Sample[1], Sample[0], Sample[2] };
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(hits, "Рок", 4, TrackReaction.Liked, Array.Empty<int>()),
            id => Array.Empty<int>());

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void TagIds_Without_Provider_Returns_Empty_Conservatively()
    {
        var result = LibraryFilter.Apply(
            Sample,
            new LibraryFilterCriteria(null, null, 0, null, new[] { 1 }),
            tagIdsOfTrack: null);

        Assert.Empty(result);
    }
}
