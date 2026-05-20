namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>
/// Связь many-to-many между Tracks и Tags. Composite primary key (TrackId, TagId),
/// каскадное удаление с обеих сторон обеспечивается на уровне FK в конфигурации.
/// </summary>
internal sealed class TrackTagEntity
{
    public int TrackId { get; set; }
    public int TagId { get; set; }

    public TrackEntity Track { get; set; } = default!;
    public TagEntity Tag { get; set; } = default!;
}
