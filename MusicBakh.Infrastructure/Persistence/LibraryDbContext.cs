using Microsoft.EntityFrameworkCore;
using MusicBakh.Infrastructure.Persistence.Configurations;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Контекст локальной библиотеки MusicBakh. В этой итерации содержит только треки,
/// в Task 10/11 к нему присоединятся история прослушиваний и KV-стор настроек.
/// </summary>
public sealed class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    internal DbSet<TrackEntity> Tracks => Set<TrackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TrackEntityConfiguration());
    }
}
