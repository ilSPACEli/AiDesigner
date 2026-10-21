using AiDesigner.Models;

namespace AiDesigner.Services.Stubs;

/// ЗАГЛУШКА 
public class StubCatalog : ICatalog
{
    private static readonly CatalogItem[] StubItems =
    {
        Item("stub-01", "Сірий диван", ItemType.SOFA, "#9E9E9E"),
        Item("stub-02", "Бежеве крісло", ItemType.ARMCHAIR, "#D8C3A5"),
        Item("stub-03", "Підлогова лампа", ItemType.LAMP, "#F5F5F5"),
        Item("stub-04", "Рослина в горщику", ItemType.PLANT, "#4CAF50")
    };

    public IReadOnlyList<CatalogItem> Items => StubItems;

    public IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count) =>
        StubItems
            .Take(count)
            .Select((item, i) => new Suggestion
            {
                Item = item,
                Rank = i + 1,
                Score = 1,
                Reason = "Тестова пропозиція"
            })
            .ToList();

    private static CatalogItem Item(string id, string name, ItemType type, string color) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        ImageFile = $"data/items/{id}.png",
        MainColor = color,
        SuitableLightings = new[] { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT },
        LicenseSource = "Заглушка каркаса"
    };
}
