using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF-конфигурация таблицы Tracks: ограничения длины строк и индексы по полям,
/// по которым в будущем будут идти фильтры (исполнитель, жанр, флаг встроенного трека).
/// </summary>
internal sealed class TrackEntityConfiguration : IEntityTypeConfiguration<TrackEntity>
{
    public void Configure(EntityTypeBuilder<TrackEntity> builder)
    {
        builder.ToTable("Tracks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Artist).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Album).IsRequired().HasMaxLength(500).HasDefaultValue(string.Empty);
        builder.Property(t => t.Genre).HasMaxLength(200);
        builder.Property(t => t.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(t => t.CoverPath).HasMaxLength(1000);

        builder.HasIndex(t => t.Artist);
        builder.HasIndex(t => t.Album);
        builder.HasIndex(t => t.Genre);
        builder.HasIndex(t => t.IsBuiltIn);
    }
}
