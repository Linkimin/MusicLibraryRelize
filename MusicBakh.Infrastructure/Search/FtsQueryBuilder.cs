namespace MusicBakh.Infrastructure.Search;

/// <summary>
/// Превращает свободную пользовательскую строку в безопасный FTS5 MATCH-запрос.
/// Логика: токенизируем по whitespace, чистим зарезервированные FTS-символы,
/// каждое слово оборачиваем в двойные кавычки и приписываем * для prefix-match,
/// склеиваем через пробел (неявный AND).
///
/// Если итог пустой — возвращаем null; вызывающий код в этом случае не делает
/// SQL-запрос вовсе. Это закрывает edge-case «пользователь ввёл только пробелы
/// или только спецсимволы» без exception.
/// </summary>
internal static class FtsQueryBuilder
{
    private const int MaxTokens = 10;

    public static string? Build(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var tokens = new List<string>(MaxTokens);
        foreach (string word in raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string cleaned = Sanitize(word);
            if (cleaned.Length == 0)
            {
                continue;
            }

            tokens.Add($"\"{cleaned}\"*");
            if (tokens.Count == MaxTokens)
            {
                break;
            }
        }

        return tokens.Count == 0 ? null : string.Join(' ', tokens);
    }

    private static string Sanitize(string word)
    {
        // Зарезервированные FTS5 символы: " * : ( ) — могут менять смысл запроса
        // или сломать парсер. Заменяем на nothing, чтобы не плодить лишних токенов.
        Span<char> buffer = stackalloc char[word.Length];
        int length = 0;
        foreach (char c in word)
        {
            if (c is '"' or '*' or ':' or '(' or ')')
            {
                continue;
            }
            buffer[length++] = c;
        }
        return length == 0 ? string.Empty : new string(buffer[..length]);
    }
}
