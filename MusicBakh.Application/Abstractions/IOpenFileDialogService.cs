namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Абстракция стандартного диалога открытия аудиофайла. Возвращает null,
/// если пользователь отменил выбор, иначе — полный путь к выбранному файлу.
/// </summary>
public interface IOpenFileDialogService
{
    string? PickAudioFile();
}
