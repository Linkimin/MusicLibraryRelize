namespace MusicBakh.Application.Contracts;

public sealed class ResolvedCover
{
    public required byte[] Bytes { get; init; }
    public required string Extension { get; init; }
}
