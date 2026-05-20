namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>
/// EF-маппинг тега. Color хранится как HEX-строка «#RRGGBB» или NULL.
/// Name имеет уникальный индекс с COLLATE NOCASE (см. TagEntityConfiguration).
/// </summary>
internal sealed class TagEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
