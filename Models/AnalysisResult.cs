namespace AiDesigner.Models;


public class AnalysisResult
{
    public IReadOnlyList<DominantColor> Colors { get; init; } = Array.Empty<DominantColor>();

    public LightingLevel Lighting { get; init; }

    public DateTime AnalyzedAt { get; init; }
}
