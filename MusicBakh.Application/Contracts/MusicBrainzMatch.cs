namespace MusicBakh.Application.Contracts;

public sealed class MusicBrainzMatch
{
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string Genre { get; init; } = string.Empty;
}
