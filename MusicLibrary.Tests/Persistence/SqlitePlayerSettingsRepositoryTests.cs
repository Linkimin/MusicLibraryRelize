using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqlitePlayerSettingsRepositoryTests
{
    [Fact]
    public void Load_Returns_Default_For_Fresh_Db()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        Assert.Equal(PlayerSettings.Default, repo.Load());
    }

    [Fact]
    public void Save_Then_Load_Returns_Same_Settings()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        var settings = new PlayerSettings(Volume: 0.42, IsMuted: true, RepeatMode: RepeatMode.Current);
        repo.Save(settings);

        Assert.Equal(settings, repo.Load());
    }

    [Fact]
    public void Save_Is_Idempotent()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        var first = new PlayerSettings(0.5, false, RepeatMode.Off);
        var second = new PlayerSettings(0.7, true, RepeatMode.Library);

        repo.Save(first);
        repo.Save(second);

        Assert.Equal(second, repo.Load());
    }
}
