using AiDesigner.Models;

namespace AiDesigner.Services.Stubs;

/// ЗАГЛУШКА

public class StubPhotoAnalyzer : IPhotoAnalyzer
{
    public async Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        // Невелика затримка, щоб на сторінці був видний індикатор.
        await Task.Delay(500, cancellationToken);

        var result = new AnalysisResult
        {
            Colors = new[]
            {
                new DominantColor { Hex = "#D9CBB4", Hue = 37, Share = 50m },
                new DominantColor { Hex = "#7A8B99", Hue = 206, Share = 30m },
                new DominantColor { Hex = "#3E3A36", Hue = 30, Share = 20m }
            },
            Lighting = LightingLevel.MODERATE,
            AnalyzedAt = DateTime.Now
        };

        return AnalysisOutcome.Success(result);
    }
}
