using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Library;

/// <summary>
/// Снимок активных фильтров библиотеки. Передаётся в <see cref="LibraryFilter.Apply"/>
/// одним пакетом, чтобы добавление новых критериев не множило сигнатуру метода.
/// Семантика «null/пусто = без фильтра», за исключением SearchHits — null значит
/// «поиск не активен», пустой список — «поиск активен, но результатов нет».
/// </summary>
public sealed record LibraryFilterCriteria(
    IReadOnlyList<Track>? SearchHits,
    string? Genre,
    int MinRating,
    TrackReaction? Reaction,
    IReadOnlyCollection<int> TagIds);
