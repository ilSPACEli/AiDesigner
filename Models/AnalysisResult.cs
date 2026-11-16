namespace AiDesigner.Models;


public class AnalysisResult
{
    public IReadOnlyList<DominantColor> Colors { get; init; } = Array.Empty<DominantColor>();

    public LightingLevel Lighting { get; init; }

    public DateTime AnalyzedAt { get; init; }

    public AnalysisMode Mode { get; init; } = AnalysisMode.BASIC;

    public RoomStyle? Style { get; init; }

    public decimal? Confidence { get; init; }
}
