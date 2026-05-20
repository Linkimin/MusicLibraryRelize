using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Pure-функция «применить критерии фильтрации к библиотеке». Вынесена из
/// MainViewModel.ApplyFilters, чтобы добавление новых критериев (рейтинг, реакция,
/// теги) не превратило ApplyFilters в спагетти и чтобы юнит-тесты могли проверить
/// комбинации без поднятия WPF.
///
/// Семантика порядка:
/// * Если задан <see cref="LibraryFilterCriteria.SearchHits"/> — порядок результата
///   равен порядку SearchHits (это сохраняет bm25-релевантность FTS-поиска).
/// * Иначе — порядок равен порядку allTracks (как было до итерации B).
///
/// Семантика мульти-тегов: OR. Трек попадает в результат, если у него есть
/// хотя бы один из перечисленных тегов. AND-режим может добавиться отдельным
/// переключателем в фильтре, если по факту использования окажется нужен.
/// </summary>
public static class LibraryFilter
{
    public static IReadOnlyList<Track> Apply(
        IReadOnlyList<Track> allTracks,
        LibraryFilterCriteria criteria,
        Func<int, IReadOnlyList<int>>? tagIdsOfTrack)
    {
        if (allTracks.Count == 0)
        {
            return Array.Empty<Track>();
        }

        IEnumerable<Track> tracks;

        if (criteria.SearchHits is not null)
        {
            // Поиск активен — сохраняем порядок hits, но фильтруем по allTracks,
            // чтобы reference equality с SelectedTrack/PlayingTrack сохранилось
            // (SearchService возвращает свежеспроецированные Track-объекты).
            var hitOrder = new Dictionary<int, int>(criteria.SearchHits.Count);
            for (int i = 0; i < criteria.SearchHits.Count; i++)
            {
                hitOrder[criteria.SearchHits[i].Id] = i;
            }
            tracks = allTracks
                .Where(t => hitOrder.ContainsKey(t.Id))
                .OrderBy(t => hitOrder[t.Id]);
        }
        else
        {
            tracks = allTracks;
        }

        if (!string.IsNullOrEmpty(criteria.Genre))
        {
            tracks = tracks.Where(t => t.Genre == criteria.Genre);
        }

        if (criteria.MinRating > 0)
        {
            tracks = tracks.Where(t => t.Rating >= criteria.MinRating);
        }

        if (criteria.Reaction is { } reaction)
        {
            tracks = tracks.Where(t => t.Reaction == reaction);
        }

        if (criteria.TagIds.Count > 0)
        {
            if (tagIdsOfTrack is null)
            {
                // Фильтр по тегам задан, но провайдер ассоциаций не прокинут —
                // консервативно ничего не возвращаем, не падаем.
                return Array.Empty<Track>();
            }
            var wanted = criteria.TagIds is HashSet<int> hs ? hs : new HashSet<int>(criteria.TagIds);
            tracks = tracks.Where(t =>
            {
                var tagIds = tagIdsOfTrack(t.Id);
                for (int i = 0; i < tagIds.Count; i++)
                {
                    if (wanted.Contains(tagIds[i])) return true;
                }
                return false;
            });
        }

        return tracks.ToList();
    }
}
