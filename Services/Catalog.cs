using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using AiDesigner.Models;
using AiDesigner.Data;
using AiDesigner.Services.Analysis;

namespace AiDesigner.Services;

public class Catalog : ICatalog
{
    // R5 / UC-03 A2: позначка для предметів, доданих лише за освітленістю (формулювання з ТЗ)
    // R3: не більше двох предметів одного типу
    public const int MaxPerType = 2;

    public const string PartialMatchReason =
        "Часткова відповідність за кольором: предмет підходить лише за освітленістю.";

    private readonly Lazy<IReadOnlyList<CatalogItem>> _itemsLoader;

    public IReadOnlyList<CatalogItem> Items => _itemsLoader.Value;

    public Catalog(IWebHostEnvironment env)
    {
        string path = Path.Combine(env.WebRootPath, CatalogLoader.DefaultRelativePath);
        _itemsLoader = new Lazy<IReadOnlyList<CatalogItem>>(() => CatalogLoader.LoadFromFile(path));
    }

    public Catalog(IReadOnlyList<CatalogItem> items)
    {
        _itemsLoader = new Lazy<IReadOnlyList<CatalogItem>>(() => items ?? new List<CatalogItem>());
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

        foreach (var item in _itemsLoader.Value)
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
