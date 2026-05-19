namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>
/// EF-маппинг записи истории прослушиваний. Связан с TrackEntity по TrackId.
/// </summary>
internal sealed class ListeningHistoryEntryEntity
{
    public long Id { get; set; }
    public int TrackId { get; set; }
    public DateTime PlayedAtUtc { get; set; }

    public TrackEntity Track { get; set; } = default!;
}
