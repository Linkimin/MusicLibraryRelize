using MusicBakh.Application.Contracts;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Читает ID3-теги (или их аналоги для wav) из локального аудиофайла.
/// Реализация не должна бросать исключения — при битых файлах возвращает пустой LocalTagInfo.
/// </summary>
public interface ITagReader
{
    LocalTagInfo Read(string filePath);
}
