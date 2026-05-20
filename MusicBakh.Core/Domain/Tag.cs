namespace MusicBakh.Core.Domain;

/// <summary>
/// Пользовательский тег-ярлык: mood/situation/что угодно — «утро», «работа», «бег».
/// Name уникален case-insensitive. Color — необязательный HEX-цвет «#RRGGBB» для
/// отрисовки чипа в UI; null означает «использовать акцентный цвет приложения».
/// </summary>
public sealed class Tag
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
}
