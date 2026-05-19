using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;

namespace MusicBakh.Infrastructure.Seeding;

/// <summary>
/// Заполняет SQLite-библиотеку набором встроенных треков. Проверяет наличие каждого
/// seed-трека по паре (Artist, Title) среди записей с IsBuiltIn=true; отсутствующие добавляет.
/// Идемпотентен и устойчив к ситуации, когда пользовательская миграция уже наполнила БД
/// своими треками — встроенные всё равно появятся.
/// </summary>
public sealed class BuiltInTrackSeeder
{
    private readonly ITrackRepository _repository;
    private readonly Func<IReadOnlyList<Track>> _seedSource;

    public BuiltInTrackSeeder(ITrackRepository repository, Func<IReadOnlyList<Track>> seedSource)
    {
        _repository = repository;
        _seedSource = seedSource;
    }

    public void SeedBuiltIns()
    {
        var existingBuiltIns = _repository.GetAll()
            .Where(t => t.IsBuiltIn)
            .Select(t => (t.Artist, t.Title))
            .ToHashSet();

        foreach (var seed in _seedSource())
        {
            if (existingBuiltIns.Contains((seed.Artist, seed.Title)))
            {
                continue;
            }
            _repository.Add(seed);
        }
    }
}
