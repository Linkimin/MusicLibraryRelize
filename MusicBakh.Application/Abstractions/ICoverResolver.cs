using MusicBakh.Application.Contracts;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Точка входа в каскад источников обложки. Гарантирует ненулевой результат:
/// если онлайн-источники не дали обложку — возвращается процедурно сгенерированная заглушка.
/// </summary>
public interface ICoverResolver
{
    Task<ResolvedCover> ResolveAsync(ResolvedMetadata metadata, CancellationToken cancellationToken);
}
