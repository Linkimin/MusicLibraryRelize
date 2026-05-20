namespace MusicLibrary.Views;

/// <summary>
/// Открывает окно управления тегами поверх главного окна. Каждый вызов создаёт
/// свежий TagsViewModel (через DI), чтобы список тегов был актуальным.
/// </summary>
public interface ITagsWindowService
{
    void Show();
}
