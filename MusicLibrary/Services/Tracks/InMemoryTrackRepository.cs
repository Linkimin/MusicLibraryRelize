using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Seeding;

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Учебный источник данных: список треков задается программно, как описано в работе.
/// Файлы при этом лежат в локальной папке приложения, поэтому проект можно запускать без внешних путей.
/// Это seed-репозиторий: добавление и удаление не поддерживаются, треки фиксированы.
/// </summary>
public sealed class InMemoryTrackRepository : ITrackRepository
{
    private readonly IReadOnlyList<Track> _tracks;

    public InMemoryTrackRepository()
    {
        _tracks = BuildSeed();
    }

    public IReadOnlyList<Track> GetAll() => _tracks;

    public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);

    public Track Add(Track track) =>
        throw new NotSupportedException("Seed-репозиторий встроенных треков не поддерживает добавление.");

    public void Update(Track track) =>
        throw new NotSupportedException("Seed-репозиторий встроенных треков не поддерживает обновление.");

    public void Remove(int id) =>
        throw new NotSupportedException("Seed-репозиторий встроенных треков не поддерживает удаление.");

    internal static IReadOnlyList<Track> BuildSeed() =>
        BuiltInTracksProvider.GetDefaults();
}
