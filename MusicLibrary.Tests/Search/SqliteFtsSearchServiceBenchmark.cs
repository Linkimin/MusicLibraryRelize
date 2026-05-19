using System.Diagnostics;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Search;
using MusicLibrary.Tests.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace MusicLibrary.Tests.Search;

/// <summary>
/// Ручной бенчмарк FTS5-поиска на 50 000 треков. Требование плана итерации B —
/// «первые результаты &lt; 100 мс на библиотеке 50k» (см. docs/roadmap-vision.md, 1.1.0 DoD).
///
/// Помечен трейтом Category=Benchmark, чтобы обычный прогон `dotnet test`
/// его пропускал — генерация датасета + измерение занимают единицы секунд.
/// Запускать руками:
///     dotnet test --filter "Category=Benchmark"
/// </summary>
public sealed class SqliteFtsSearchServiceBenchmark
{
    private const int TrackCount = 50_000;

    private readonly ITestOutputHelper _output;

    public SqliteFtsSearchServiceBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Search_On_50k_Tracks_Under_100ms()
    {
        using var factory = new MigratedSqliteDbContextFactory();

        // Seed 50k треков. AddRangeAsync + SaveChanges в одном transaction'е дёшево.
        var random = new Random(42);
        var artists = new[] { "Queen", "Pink Floyd", "Muse", "Radiohead", "Beatles", "Цой", "Кино", "Земфира", "Mother Mother", "MYTH ROID" };
        var titles  = new[] { "Time", "Money", "Radio", "Hayloft", "Star", "Light", "Dawn", "Echo", "Forever", "Memory" };
        var genres  = new[] { "Рок", "Инди", "Поп", "Альт", "Электро" };

        Stopwatch sw = Stopwatch.StartNew();
        using (var ctx = factory.CreateContext())
        {
            for (int i = 0; i < TrackCount; i++)
            {
                ctx.Tracks.Add(new TrackEntity
                {
                    Title    = $"{titles[random.Next(titles.Length)]} #{i}",
                    Artist   = artists[random.Next(artists.Length)],
                    Album    = $"Album {i / 12}",
                    Genre    = genres[random.Next(genres.Length)],
                    FilePath = $"track-{i}.mp3"
                });

                // Батч-флаш каждые 5 000 — иначе change tracker раздувается.
                if (i % 5000 == 4999)
                {
                    ctx.SaveChanges();
                    ctx.ChangeTracker.Clear();
                }
            }
            ctx.SaveChanges();
        }
        sw.Stop();
        _output.WriteLine($"Seed {TrackCount} tracks: {sw.ElapsedMilliseconds} ms");

        var service = new SqliteFtsSearchService(factory.CreateContext);

        // Warm-up: первый запрос компилирует план; меряем уже горячий.
        _ = service.Search("queen");

        var queries = new[] { "queen", "time", "альбом", "muse radio", "memory" };
        long total = 0;
        foreach (var q in queries)
        {
            sw.Restart();
            var hits = service.Search(q, limit: 100);
            sw.Stop();
            _output.WriteLine($"Search '{q}' → {hits.Count} hits in {sw.ElapsedMilliseconds} ms");
            total += sw.ElapsedMilliseconds;
        }

        long avg = total / queries.Length;
        _output.WriteLine($"Average over {queries.Length} queries: {avg} ms");

        Assert.True(avg < 100,
            $"FTS-поиск на {TrackCount} треков должен укладываться в 100 мс в среднем; получено {avg} мс. " +
            "Если упало — обновить план/changelog 1.0.2 с актуальной цифрой и завести задачу.");
    }
}
