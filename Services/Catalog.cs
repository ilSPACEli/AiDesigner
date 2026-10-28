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
    private readonly IReadOnlyList<CatalogItem> _items;

    public IReadOnlyList<CatalogItem> Items => _items;

    public Catalog(IWebHostEnvironment env)
    {
        string path = Path.Combine(env.WebRootPath, CatalogLoader.DefaultRelativePath);
        _items = CatalogLoader.LoadFromFile(path);
    }

    public Catalog(IReadOnlyList<CatalogItem> items)
    {
        _items = items ?? new List<CatalogItem>();
    }

    public IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count)
    {
        var candidates = new List<Suggestion>();
        var fallbacks = new List<Suggestion>();

        var topColors = analysis.Colors?.Take(2).ToList() ?? new List<DominantColor>();

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
                    Reason = "Часткова відповідність (лише за рівнем освітлення)."
                });
            }
        }

        var sortedCandidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Item.Id)
            .ToList();

        var finalSuggestions = new List<Suggestion>();
        var typeCounts = new Dictionary<ItemType, int>();

        foreach (var candidate in sortedCandidates)
        {
            if (finalSuggestions.Count >= count) break;

            typeCounts.TryGetValue(candidate.Item.Type, out int currentTypeCount);
            if (currentTypeCount < 2)
            {
                finalSuggestions.Add(candidate);
                typeCounts[candidate.Item.Type] = currentTypeCount + 1;
            }
        }

        if (finalSuggestions.Count < 3)
        {
            var sortedFallbacks = fallbacks.OrderBy(f => f.Item.Id).ToList();
            foreach (var fallback in sortedFallbacks)
            {
                if (finalSuggestions.Count >= count) break;

                typeCounts.TryGetValue(fallback.Item.Type, out int currentTypeCount);
                if (currentTypeCount < 2)
                {
                    finalSuggestions.Add(fallback);
                    typeCounts[fallback.Item.Type] = currentTypeCount + 1;
                }
            }
        }

        // Створюємо нові об'єкти Suggestion, щоб задати Rank (init-only)
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

    private (int Score, string Reason) EvaluateItemColor(CatalogItem item, List<DominantColor> topColors)
    {
        bool isItemNeutral = ColorHelper.IsNeutral(item.MainColor);
        double itemHue = GetHue(item.MainColor);

        foreach (var domColor in topColors)
        {
            if (ColorHelper.IsNeutral(domColor.Hex))
                continue;

            if (ColorHelper.IsAnalogous(itemHue, domColor.Hue))
                return (2, "Аналогова гармонія з домінантним кольором інтер'єру.");

            if (ColorHelper.IsComplementary(itemHue, domColor.Hue))
                return (2, "Комплементарна гармонія (яскравий акцент) до кольору інтер'єру.");
        }

        if (isItemNeutral)
            return (1, "Універсальний нейтральний колір, що підходить для цієї кімнати.");

        return (0, string.Empty);
    }

    private double GetHue(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return 0;

        double r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255.0;
        double g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0;
        double b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        if (max == min) return 0;

        double d = max - min;
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) 
               : max == g ? (b - r) / d + 2 
               : (r - g) / d + 4;

        return h * 60;
    }
}