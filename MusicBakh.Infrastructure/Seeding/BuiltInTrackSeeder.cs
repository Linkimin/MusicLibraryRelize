using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;

namespace MusicBakh.Infrastructure.Seeding;

/// <summary>
/// Заполняет SQLite-библиотеку набором встроенных треков при первом запуске.
/// Если в репозитории уже есть хотя бы один трек — ничего не делает (идемпотентность).
/// Источник seed-треков параметризован, чтобы тесты могли подставить свой набор.
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

    public void SeedIfEmpty()
    {
        if (_repository.GetAll().Count > 0)
        {
            return;
        }

        foreach (var t in _seedSource())
        {
            _repository.Add(t);
        }
    }
}
