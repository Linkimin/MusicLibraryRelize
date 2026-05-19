using System.IO;
using MusicBakh.Application.Abstractions;

namespace MusicBakh.Infrastructure.FileSystem;

/// <summary>
/// Стандартные пути хранения: %LocalAppData%\MusicLibrary с подкаталогами Music и Covers.
/// Каталоги создаются при первом обращении.
/// </summary>
public sealed class LocalAppDataMusicStoragePaths : IMusicStoragePaths
{
    public LocalAppDataMusicStoragePaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicLibrary");
        MusicDirectory = Path.Combine(RootDirectory, "Music");
        CoversDirectory = Path.Combine(RootDirectory, "Covers");

        Directory.CreateDirectory(MusicDirectory);
        Directory.CreateDirectory(CoversDirectory);
    }

    public string RootDirectory { get; }
    public string MusicDirectory { get; }
    public string CoversDirectory { get; }
}
