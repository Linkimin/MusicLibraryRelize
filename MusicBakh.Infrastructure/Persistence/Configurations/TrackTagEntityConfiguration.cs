using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация связки many-to-many TrackTags: composite key и каскадное удаление
/// с обеих сторон. Удалили трек — связи ушли; удалили тег — он отвязался.
/// </summary>
internal sealed class TrackTagEntityConfiguration : IEntityTypeConfiguration<TrackTagEntity>
{
    public void Configure(EntityTypeBuilder<TrackTagEntity> builder)
    {
        builder.ToTable("TrackTags");
        builder.HasKey(tt => new { tt.TrackId, tt.TagId });

        builder.HasOne(tt => tt.Track)
            .WithMany()
            .HasForeignKey(tt => tt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tt => tt.Tag)
            .WithMany()
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tt => tt.TagId); // для быстрого «дай все треки этого тега»
    }
}
