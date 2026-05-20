using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация таблицы Tags: уникальный индекс по Name с COLLATE NOCASE — чтобы
/// «утро» и «Утро» считались одним и тем же тегом.
/// </summary>
internal sealed class TagEntityConfiguration : IEntityTypeConfiguration<TagEntity>
{
    public void Configure(EntityTypeBuilder<TagEntity> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Color).HasMaxLength(9); // "#RRGGBBAA" максимум

        // SQLite поддерживает COLLATE NOCASE в индексе. EF-аннотации не позволяют
        // указать collation у индекса, поэтому добавляем «сырым» SQL прямо в миграции
        // (в EF-конфигурации это будет обычный unique index, а raw SQL — в Up()).
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
