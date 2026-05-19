using MusicBakh.Core.Abstractions;

namespace MusicBakh.Infrastructure.Time;

/// <summary>
/// Реализация IClock на базе DateTime.UtcNow. Используется в production-коде.
/// Тесты подменяют IClock своей фейковой реализацией.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
