using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Models;

namespace AiDesigner.Services.Stubs;

// Заглушка добору без HTTP і БД: завжди повертає ті самі 4 предмети (замінила StubCatalog з v1).
public sealed class StubSuggestionClient : ISuggestionClient
{
    private static readonly LightingLevel[] AllLevels = { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT };

    public static IReadOnlyList<CatalogItem> Items { get; } = new[]
    {
        Item("stub-01", "Сірий диван", ItemType.SOFA, "#9E9E9E"),
        Item("stub-02", "Бежеве крісло", ItemType.ARMCHAIR, "#D8C3A5"),
        Item("stub-03", "Підлогова лампа", ItemType.LAMP, "#F5F5F5"),
        Item("stub-04", "Рослина в горщику", ItemType.PLANT, "#4CAF50")
    };

    public Task<SuggestionClientResult> SuggestAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var suggestions = Items
            .Select((item, i) => SuggestionDto.From(new Suggestion
            {
                Item = item,
                Rank = i + 1,
                Score = 1,
                Reason = "Тестова пропозиція"
            }))
            .ToList();

        return Task.FromResult(SuggestionClientResult.Success(suggestions));
    }

    private static CatalogItem Item(string id, string name, ItemType type, string hex) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        ImageFile = $"data/items/{id}.png",
        MainColor = hex,
        SuitableLightings = AllLevels,
        LicenseSource = "Заглушка каркаса"
    };
}
