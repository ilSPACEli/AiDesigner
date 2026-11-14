using AiDesigner.Models;
using AiDesigner.Services.Analysis;

namespace AiDesigner.Services.Suggestions;

// Правила добору пропозицій R1–R5 (UC-03; CR-01, CR-03). Перенесено без змін логіки з Services/Catalog.cs (v1):
// тепер це чистий клас без файлів і БД — предмети передає SuggestionService (з ICatalogRepository).
public sealed class SuggestionRules
{
    // R5 / UC-03 A2: позначка для предметів, доданих лише за освітленістю (формулювання з ТЗ)
    // R3: не більше двох предметів одного типу
    public const int MaxPerType = 2;

    public const string PartialMatchReason =
        "Часткова відповідність за кольором: предмет підходить лише за освітленістю.";

    private readonly IReadOnlyList<CatalogItem> _items;

    public SuggestionRules(IReadOnlyList<CatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    public IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count)
    {
        var candidates = new List<Suggestion>();
        var fallbacks = new List<Suggestion>();

        var topColors = analysis.Colors?
            .Where(c => !ColorHelper.IsNeutral(c.Hex))
            .OrderByDescending(c => c.Share)
            .Take(2)
            .ToList() ?? new List<DominantColor>();

        foreach (var item in _items)
        {
            if (item.SuitableLightings == null || !item.SuitableLightings.Contains(analysis.Lighting))
                continue;

            var (score, reason) = EvaluateItemColor(item, topColors);

            if (score > 0)
            {
                candidates.Add(new Suggestion
                {
                    Item = item,
                    Score = score,
                    Reason = reason
                });
            }
            else
            {
                fallbacks.Add(new Suggestion
                {
                    Item = item,
                    Score = 0,
                    Reason = PartialMatchReason
                });
            }
        }

        var sortedCandidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Item.Id)
            .ToList();

        var finalSuggestions = new List<Suggestion>();
        var typeCounts = new Dictionary<ItemType, int>();

        // R3 (CR-03): спершу різні типи, другий предмет того самого типу — лише якщо типів не вистачає
        PickDiverse(sortedCandidates, finalSuggestions, typeCounts, count);

        // R4: у переліку вищий бал — вище; за рівних балів лишається порядок добору (стабільне сортування)
        finalSuggestions = finalSuggestions.OrderByDescending(s => s.Score).ToList();

        if (finalSuggestions.Count < 3)
        {
            var sortedFallbacks = fallbacks.OrderBy(f => f.Item.Id).ToList();
            PickDiverse(sortedFallbacks, finalSuggestions, typeCounts, count);
        }

        var rankedSuggestions = new List<Suggestion>();
        for (int i = 0; i < finalSuggestions.Count; i++)
        {
            var s = finalSuggestions[i];
            rankedSuggestions.Add(new Suggestion
            {
                Item = s.Item,
                Score = s.Score,
                Reason = s.Reason,
                Rank = i + 1
            });
        }

        return rankedSuggestions;
    }

    // Двопрохідний добір за R3: перший прохід бере лише предмети нових типів,
    // другий — другий предмет того самого типу (до MaxPerType), поки є місце.
    private static void PickDiverse(IReadOnlyList<Suggestion> ordered, List<Suggestion> result,
        Dictionary<ItemType, int> typeCounts, int count)
    {
        for (var limit = 1; limit <= MaxPerType; limit++)
        {
            foreach (var suggestion in ordered)
            {
                if (result.Count >= count)
                    return;
                if (result.Contains(suggestion))
                    continue;

                typeCounts.TryGetValue(suggestion.Item.Type, out var taken);
                if (taken >= limit)
                    continue;

                result.Add(suggestion);
                typeCounts[suggestion.Item.Type] = taken + 1;
            }
        }
    }

    private (int Score, string Reason) EvaluateItemColor(CatalogItem item, List<DominantColor> topColors)
    {
        if (ColorHelper.IsNeutral(item.MainColor))
            return (1, "Універсальний нейтральний колір, що підходить для цієї кімнати.");

        double itemHue = ColorHelper.GetHue(item.MainColor);

        foreach (var domColor in topColors)
        {

            if (ColorHelper.IsAnalogous(itemHue, domColor.Hue))
                return (2, "Аналогова гармонія з домінантним кольором інтер'єру.");

            if (ColorHelper.IsComplementary(itemHue, domColor.Hue))
                return (2, "Комплементарна гармонія (яскравий акцент) до кольору інтер'єру.");
        }

        return (0, string.Empty);
    }
}
