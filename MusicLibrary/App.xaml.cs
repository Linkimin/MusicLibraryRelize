using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicBakh.Infrastructure.DependencyInjection;
using MusicBakh.Infrastructure.Migration;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Seeding;
using MusicLibrary.DependencyInjection;

namespace MusicLibrary;

/// <summary>
/// Точка входа WPF-приложения. Поднимает DI-host, прогоняет миграции SQLite, мигрирует legacy
/// userTracks.json (если он есть), наполняет встроенные треки и открывает MainWindow.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Стабильный AppUserModelID нужен, чтобы Windows корректно группировал приложение в taskbar.
        _ = SetCurrentProcessExplicitAppUserModelID("MusicBakh.Player");

        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMusicBakhInfrastructure(LibraryDbContextOptions.Default);
                services.AddMusicBakhPresentation();
            })
            .Build();

        InitializeDatabase(_host.Services);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void InitializeDatabase(IServiceProvider services)
    {
        // 1) Создаём БД и применяем миграции.
        var contextFactory = services.GetRequiredService<IDbContextFactory<LibraryDbContext>>();
        using (var ctx = contextFactory.CreateDbContext())
        {
            ctx.Database.Migrate();
        }

        // 2) Мигрируем legacy userTracks.json при первом запуске после апгрейда с 1.0.x.
        var migration = services.GetRequiredService<JsonToSqliteMigrationService>();
        migration.Run();

        // 3) Заполняем встроенные треки, если БД пуста.
        var seeder = services.GetRequiredService<BuiltInTrackSeeder>();
        seeder.SeedIfEmpty();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
