using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;

namespace MusicBakh.Infrastructure.Seeding;

/// <summary>
/// Заполняет SQLite-библиотеку набором встроенных треков. Логика двухэтапная:
///   1) Для каждого seed-трека ищем существующий по паре (Artist, Title) среди записей
///      с IsBuiltIn=true.
///   2) Если нашли — сверяем FilePath/CoverPath с актуальными (AppContext.BaseDirectory
///      мог измениться, например при переезде сборки в bin/Release или другой каталог),
///      и при расхождении обновляем пути через ITrackRepository.Update. Это лечит сценарий
///      «файл не найден» у встроенных треков после переезда сборки.
///   3) Если seed-трека в БД нет — добавляем.
/// Идемпотентен.
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
            .ToDictionary(t => (t.Artist, t.Title));

        foreach (var seed in _seedSource())
        {
            if (existingBuiltIns.TryGetValue((seed.Artist, seed.Title), out var existing))
            {
                bool pathsDiverged =
                    !string.Equals(existing.FilePath,  seed.FilePath,  StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.CoverPath, seed.CoverPath, StringComparison.OrdinalIgnoreCase);

                if (pathsDiverged)
                {
                    _repository.Update(new Track
                    {
                        Id = existing.Id,
                        Title = seed.Title,
                        Artist = seed.Artist,
                        Album = seed.Album,
                        Genre = seed.Genre,
                        Duration = seed.Duration,
                        FilePath = seed.FilePath,
                        CoverPath = seed.CoverPath,
                        IsBuiltIn = true
                    });
                }
                continue;
            }
            _repository.Add(seed);
        }
    }
}
