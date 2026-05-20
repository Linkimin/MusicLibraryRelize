using MusicBakh.Core.Domain;

namespace MusicLibrary.ViewModels;

/// <summary>
/// Обёртка тега для отображения как чип в фильтр-панели. Хранит IsSelected
/// рядом с самим тегом, чтобы биндинги в DataTemplate могли реагировать на
/// выбор без MultiBinding-а к коллекции SelectedTagIds.
/// </summary>
public sealed class TagFilterItem : ViewModelBase
{
    private bool _isSelected;

    public TagFilterItem(Tag tag, bool isSelected = false)
    {
        Tag = tag;
        _isSelected = isSelected;
    }

    public Tag Tag { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
