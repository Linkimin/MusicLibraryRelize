using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Migration.Legacy;

#pragma warning disable CS0618 // CompositeTrackRepository использует legacy IUserTrackStorage до Task 16.

namespace MusicLibrary.Services.Tracks;

/// <summary>
/// Объединяет захардкоженные «эталонные» треки и пользовательские из JSON-хранилища.
/// Захардкоженные всегда идут первыми и сохраняют свои Id (1..N). Пользовательские
/// сохраняют свой Id, если он не пересекается; иначе получают новый, начиная с N+1.
/// Это нужно потому, что в первой версии приложения Id не были стабильными при
/// удалении треков, и теоретически в JSON могли остаться старые значения.
/// </summary>
public sealed class CompositeTrackRepository : ITrackRepository
{
    private readonly ITrackRepository _builtIn;
    private readonly IUserTrackStorage _storage;

    public CompositeTrackRepository(ITrackRepository builtIn, IUserTrackStorage storage)
    {
        _builtIn = builtIn;
        _storage = storage;
    }

    public IReadOnlyList<Track> GetAll()
    {
        IReadOnlyList<Track> builtInTracks = _builtIn.GetAll();
        int nextId = builtInTracks.Count == 0 ? 1 : builtInTracks.Max(t => t.Id) + 1;

        var taken = new HashSet<int>(builtInTracks.Select(t => t.Id));
        var result = new List<Track>(builtInTracks);

        foreach (UserTrack user in _storage.Load())
        {
            int id = taken.Contains(user.Id) ? nextId++ : user.Id;
            taken.Add(id);

            result.Add(new Track
            {
                Id = id,
                Title = user.Title,
                Artist = user.Artist,
                // Legacy JSON-формат UserTrack не несёт Album — пользовательские треки
                // оттуда приходят с пустым альбомом. Полноценное значение появится после
                // ре-импорта (TagLib читает tag.Album).
                Album = string.Empty,
                Genre = user.Genre,
                Duration = TimeSpan.FromSeconds(user.DurationSeconds),
                FilePath = user.FilePath,
                CoverPath = user.CoverPath
            });

            if (id >= nextId)
            {
                nextId = id + 1;
            }
        }

        return result;
    }

    // Допустимое решение, пока CompositeTrackRepository не заменён SqliteTrackRepository в Task 11:
    // FindById вызывает GetAll и просматривает результат — на текущих объёмах библиотеки накладные расходы пренебрежимы.
    public Track? FindById(int id) => GetAll().FirstOrDefault(t => t.Id == id);

    public Track Add(Track track) =>
        throw new NotSupportedException("CompositeTrackRepository не поддерживает добавление в этой итерации.");

    public void Remove(int id) =>
        throw new NotSupportedException("CompositeTrackRepository не поддерживает удаление в этой итерации.");
}

#pragma warning restore CS0618
