namespace MusicBakh.Application.Abstractions;

public interface ISaveFileDialogService
{
    string? PickSavePath(string suggestedFileName);
}
