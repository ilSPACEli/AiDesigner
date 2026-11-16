using AiDesigner.Models;

namespace AiDesigner.Services.Style;

public sealed record ModeAvailability(AnalysisMode Mode, bool IsAvailable, string? Reason)
{
    public static ModeAvailability Available(AnalysisMode mode) => new(mode, true, null);

    public static ModeAvailability Unavailable(AnalysisMode mode, string reason) => new(mode, false, reason);
}

public interface IStyleAnalyzer
{
    AnalysisMode Mode { get; }

    Task<ModeAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<string> AnalyzeStyleAsync(byte[] image, CancellationToken cancellationToken = default);
}
