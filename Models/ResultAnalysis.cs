namespace AiDesigner.Models;

public class ResultAnalysis
{
    public int SavedResultId { get; init; }

    public AnalysisMode Mode { get; init; }

    public LightingLevel Lighting { get; init; }

    public IReadOnlyList<DominantColor> Colors { get; init; } = Array.Empty<DominantColor>();

    public RoomStyle? Style { get; init; }

    public decimal? Confidence { get; init; }
}
