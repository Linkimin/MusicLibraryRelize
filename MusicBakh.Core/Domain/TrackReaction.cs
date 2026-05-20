namespace MusicBakh.Core.Domain;

/// <summary>
/// Реакция пользователя на трек: «нет реакции», «лайк», «дизлайк». Хранится в
/// БД как INTEGER (значения зафиксированы явно, чтобы можно было реляционно
/// фильтровать без джойна на справочник).
/// </summary>
public enum TrackReaction
{
    None = 0,
    Liked = 1,
    Disliked = 2
}
