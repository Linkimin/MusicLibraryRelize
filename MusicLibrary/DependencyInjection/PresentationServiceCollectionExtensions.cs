using Microsoft.Extensions.DependencyInjection;
using MusicBakh.Application.Abstractions;
using MusicLibrary.Services.Covers;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.ViewModels;
using MusicLibrary.Views;

namespace MusicLibrary.DependencyInjection;

/// <summary>
/// Регистрация WPF-зависимых сервисов: аудиоплеер, файловые диалоги, брендированный
/// диалог подтверждения, процедурный генератор обложек (использует WPF), ViewModels
/// и MainWindow.
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddMusicBakhPresentation(this IServiceCollection services)
    {
        // WPF-специфичные сервисы.
        services.AddSingleton<IAudioPlayerService, MediaPlayerAudioService>();
        services.AddSingleton<IOpenFileDialogService, OpenFileDialogService>();
        services.AddSingleton<ISaveFileDialogService, SaveFileDialogService>();
        services.AddSingleton<IConfirmationService, ConfirmationDialogService>();
        services.AddSingleton<IProceduralCoverGenerator, ProceduralCoverGenerator>();

        // Диалоги и сервисы окон.
        services.AddSingleton<IAddTrackDialogService, AddTrackDialogService>();
        services.AddSingleton<IStatsWindowService, StatsWindowService>();

        // ViewModels (transient — каждое окно получает свой инстанс).
        services.AddTransient<MainViewModel>();
        services.AddTransient<AddTrackViewModel>();
        services.AddTransient<StatsViewModel>();

        // Окна.
        services.AddTransient<MainWindow>();

        return services;
    }
}
