using AiDesigner.Models;

namespace AiDesigner.Services.Suggestions;

// Чим закінчився добір. Кожне значення відповідає статусу HTTP (docs/api.md, розд. 1).
public enum SuggestionStatus
{
    // 200: є пропозиції.
    Ok,

    // 422: придатних предметів немає (UC-03 A2 / R5).
    NoSuitableItems,

    // 503: каталог недоступний — БД не відповідає або таблиця порожня (UC-03 E1).
    CatalogUnavailable
}

// Очікувані відмови повертаються як значення, а не як винятки (правило проєкту).
public sealed record SuggestionOutcome(SuggestionStatus Status, IReadOnlyList<Suggestion> Suggestions)
{
    public static SuggestionOutcome Ok(IReadOnlyList<Suggestion> suggestions) => new(SuggestionStatus.Ok, suggestions);

    public static SuggestionOutcome NoSuitableItems() => new(SuggestionStatus.NoSuitableItems, Array.Empty<Suggestion>());

    public static SuggestionOutcome CatalogUnavailable() => new(SuggestionStatus.CatalogUnavailable, Array.Empty<Suggestion>());
}
