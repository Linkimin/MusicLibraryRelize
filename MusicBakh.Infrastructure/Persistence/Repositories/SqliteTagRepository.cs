using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// Репозиторий тегов поверх LibraryDbContext. Attach/Detach намеренно идемпотентны
/// (UI часто шлёт повторные вызовы при drag-drop / повторном клике), уникальность
/// имени обеспечивается на уровне схемы COLLATE NOCASE.
/// </summary>
public sealed class SqliteTagRepository : ITagRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;

    public SqliteTagRepository(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Tag> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Tags.OrderBy(t => t.Name).Select(MapToDomain).ToList();
    }

    public Tag? FindById(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == id);
        return entity is null ? null : MapToDomain(entity);
    }

    public Tag Add(Tag tag)
    {
        using var ctx = _contextFactory();
        var entity = new TagEntity { Name = tag.Name.Trim(), Color = tag.Color };
        ctx.Tags.Add(entity);
        try
        {
            ctx.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sx && sx.SqliteErrorCode == 19)
        {
            // 19 = SQLITE_CONSTRAINT (включает UNIQUE-нарушение).
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.", ex);
        }

        return MapToDomain(entity);
    }

    public void Update(Tag tag)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == tag.Id);
        if (entity is null)
        {
            return;
        }
        entity.Name = tag.Name.Trim();
        entity.Color = tag.Color;
        try
        {
            ctx.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sx && sx.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.", ex);
        }
    }

    public void Remove(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tags.FirstOrDefault(t => t.Id == id);
        if (entity is null)
        {
            return;
        }
        ctx.Tags.Remove(entity);
        ctx.SaveChanges();
    }

    public IReadOnlyList<Tag> GetTagsForTrack(int trackId)
    {
        using var ctx = _contextFactory();
        return ctx.TrackTags
            .Where(tt => tt.TrackId == trackId)
            .Select(tt => tt.Tag)
            .OrderBy(t => t.Name)
            .Select(t => MapToDomain(t))
            .ToList();
    }

    public void AttachTag(int trackId, int tagId)
    {
        using var ctx = _contextFactory();
        // INSERT OR IGNORE — идемпотентно: повторный вызов не дублирует и не падает.
        // Заодно защищает от нарушения FK, если переданы несуществующие Id'ы:
        // SqliteException 787 (FOREIGN KEY constraint failed) пробросится.
        ctx.Database.ExecuteSqlInterpolated($@"
            INSERT OR IGNORE INTO TrackTags (TrackId, TagId) VALUES ({trackId}, {tagId})");
    }

    public void DetachTag(int trackId, int tagId)
    {
        using var ctx = _contextFactory();
        // DELETE — silent no-op если связи нет.
        ctx.Database.ExecuteSqlInterpolated($@"
            DELETE FROM TrackTags WHERE TrackId = {trackId} AND TagId = {tagId}");
    }

    private static Tag MapToDomain(TagEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Color = e.Color
    };
}
