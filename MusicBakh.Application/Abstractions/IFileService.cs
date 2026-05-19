using MusicBakh.Core.Domain;

namespace MusicBakh.Application.Abstractions;

public interface IFileService
{
    bool Exists(string path);
    OperationResult Copy(string sourcePath, string targetPath, bool overwrite);
    string GetFileName(string path);
}
