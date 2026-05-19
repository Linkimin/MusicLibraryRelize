namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Пути, по которым приложение хранит пользовательские аудиофайлы и обложки.
/// Замещает MusicDirectory/CoversDirectory из legacy IUserTrackStorage.
/// </summary>
public interface IMusicStoragePaths
{
    string RootDirectory { get; }
    string MusicDirectory { get; }
    string CoversDirectory { get; }
}
