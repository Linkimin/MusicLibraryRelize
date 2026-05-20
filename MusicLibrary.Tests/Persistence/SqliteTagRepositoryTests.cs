using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqliteTagRepositoryTests
{
    [Fact]
    public void Add_AssignsId_And_GetAll_Returns_It()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTagRepository(factory.CreateContext);

        var saved = repo.Add(new Tag { Name = "утро", Color = "#FFAA00" });

        Assert.True(saved.Id > 0);
        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("утро", all[0].Name);
        Assert.Equal("#FFAA00", all[0].Color);
    }

    [Fact]
    public void Add_Rejects_Duplicate_Name_CaseInsensitive_For_Ascii()
    {
        // ВАЖНО: SQLite COLLATE NOCASE — ASCII-only. Для латиницы работает,
        // для кириллицы «утро» и «Утро» считаются разными. Полная case-
        // insensitive нормализация для кириллицы потребует ICU-расширения и
        // отнесена в backlog 1.0.4+.
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTagRepository(factory.CreateContext);
        repo.Add(new Tag { Name = "Sport" });

        var ex = Assert.Throws<InvalidOperationException>(() => repo.Add(new Tag { Name = "SPORT" }));
        Assert.Contains("уже существует", ex.Message);
    }

    [Fact]
    public void Add_Allows_Duplicate_Cyrillic_Names_Due_To_Sqlite_Limitation()
    {
        // Документирующий тест: фиксирует факт известного ограничения, чтобы
        // регрессия (например, переезд на ICU collation) была заметна — этот
        // тест начнёт падать, а мы поймём что фичу подняли и пора снять
        // ограничение в comments/docs.
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTagRepository(factory.CreateContext);
        repo.Add(new Tag { Name = "утро" });

        // НЕ throws: ICU не включён → кириллический case-fold не работает.
        repo.Add(new Tag { Name = "Утро" });

        Assert.Equal(2, repo.GetAll().Count);
    }

    [Fact]
    public void Update_Renames_Tag()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        var repo = new SqliteTagRepository(factory.CreateContext);
        var saved = repo.Add(new Tag { Name = "Bega", Color = null });

        repo.Update(new Tag { Id = saved.Id, Name = "Бег", Color = "#00FF00" });

        var found = repo.FindById(saved.Id);
        Assert.Equal("Бег", found!.Name);
        Assert.Equal("#00FF00", found.Color);
    }

    [Fact]
    public void Remove_Deletes_Tag_And_Cascades_Associations()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        SeedTrack(factory, 1);

        var repo = new SqliteTagRepository(factory.CreateContext);
        var tag = repo.Add(new Tag { Name = "Work" });
        repo.AttachTag(trackId: 1, tagId: tag.Id);

        repo.Remove(tag.Id);

        Assert.Empty(repo.GetAll());
        Assert.Empty(repo.GetTagsForTrack(1));
    }

    [Fact]
    public void AttachTag_Is_Idempotent()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        SeedTrack(factory, 1);
        var repo = new SqliteTagRepository(factory.CreateContext);
        var tag = repo.Add(new Tag { Name = "Sport" });

        repo.AttachTag(1, tag.Id);
        repo.AttachTag(1, tag.Id); // повтор не должен ни падать, ни дублировать

        var tagsForTrack = repo.GetTagsForTrack(1);
        Assert.Single(tagsForTrack);
        Assert.Equal("Sport", tagsForTrack[0].Name);
    }

    [Fact]
    public void DetachTag_Is_Idempotent_When_Missing()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        SeedTrack(factory, 1);
        var repo = new SqliteTagRepository(factory.CreateContext);
        var tag = repo.Add(new Tag { Name = "Sport" });

        repo.DetachTag(1, tag.Id); // связи нет — silent no-op
        repo.AttachTag(1, tag.Id);
        repo.DetachTag(1, tag.Id);
        repo.DetachTag(1, tag.Id); // снова silent

        Assert.Empty(repo.GetTagsForTrack(1));
    }

    [Fact]
    public void Removing_Track_Cascades_To_TrackTags()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        SeedTrack(factory, 1);
        var tagRepo = new SqliteTagRepository(factory.CreateContext);
        var trackRepo = new SqliteTrackRepository(factory.CreateContext);
        var tag = tagRepo.Add(new Tag { Name = "Morning" });
        tagRepo.AttachTag(1, tag.Id);

        trackRepo.Remove(1);

        // Связь должна уйти каскадом FK, но сам тег — остаться.
        Assert.Empty(tagRepo.GetTagsForTrack(1));
        Assert.Single(tagRepo.GetAll());
    }

    [Fact]
    public void GetTagsForTrack_Returns_Only_Tags_For_That_Track()
    {
        using var factory = new MigratedSqliteDbContextFactory();
        SeedTrack(factory, 1);
        SeedTrack(factory, 2);
        var repo = new SqliteTagRepository(factory.CreateContext);
        var a = repo.Add(new Tag { Name = "A" });
        var b = repo.Add(new Tag { Name = "B" });
        repo.AttachTag(1, a.Id);
        repo.AttachTag(1, b.Id);
        repo.AttachTag(2, b.Id);

        var tags1 = repo.GetTagsForTrack(1);
        var tags2 = repo.GetTagsForTrack(2);

        Assert.Equal(2, tags1.Count);
        Assert.Single(tags2);
        Assert.Equal("B", tags2[0].Name);
    }

    private static void SeedTrack(MigratedSqliteDbContextFactory factory, int id)
    {
        using var ctx = factory.CreateContext();
        ctx.Tracks.Add(new TrackEntity { Id = id, Title = $"Track {id}", Artist = "X", FilePath = $"{id}.mp3" });
        ctx.SaveChanges();
    }
}
