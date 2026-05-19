namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Абстракция системного времени, чтобы тесты могли подменять DateTime.UtcNow.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
