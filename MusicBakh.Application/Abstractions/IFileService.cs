using MusicBakh.Core.Domain;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Файловые операции, скрывающие System.IO от UI и use-case-слоя.
/// </summary>
public interface IFileService
{
    bool Exists(string path);
    OperationResult Copy(string sourcePath, string targetPath, bool overwrite);
    string GetFileName(string path);

    /// <summary>
    /// Удаляет файл best-effort: если файл отсутствует — тихо ничего не делает,
    /// если занят другим процессом — возвращает Error, не бросает исключение.
    /// </summary>
    OperationResult Delete(string path);
}
