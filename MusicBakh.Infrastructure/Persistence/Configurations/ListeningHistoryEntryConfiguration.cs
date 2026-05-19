using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация таблицы ListeningHistory: индекс по времени для сортировки последних,
/// каскадное удаление при удалении трека.
/// </summary>
internal sealed class ListeningHistoryEntryConfiguration : IEntityTypeConfiguration<ListeningHistoryEntryEntity>
{
    public void Configure(EntityTypeBuilder<ListeningHistoryEntryEntity> builder)
    {
        builder.ToTable("ListeningHistory");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.PlayedAtUtc).IsRequired();
        builder.HasIndex(h => h.PlayedAtUtc);
        builder.HasIndex(h => h.TrackId);

        builder.HasOne(h => h.Track)
            .WithMany()
            .HasForeignKey(h => h.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
