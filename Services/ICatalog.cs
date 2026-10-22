using AiDesigner.Models;

namespace AiDesigner.Services;


public interface ICatalog
{
    IReadOnlyList<CatalogItem> Items { get; }

    IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count);
}
