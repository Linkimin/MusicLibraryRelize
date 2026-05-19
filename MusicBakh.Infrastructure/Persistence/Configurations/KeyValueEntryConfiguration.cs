using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация KV-таблицы: Key — primary key, Value — обязательное поле.
/// </summary>
internal sealed class KeyValueEntryConfiguration : IEntityTypeConfiguration<KeyValueEntryEntity>
{
    public void Configure(EntityTypeBuilder<KeyValueEntryEntity> builder)
    {
        builder.ToTable("KeyValueStore");
        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key).HasMaxLength(200);
        builder.Property(k => k.Value).IsRequired();
    }
}
