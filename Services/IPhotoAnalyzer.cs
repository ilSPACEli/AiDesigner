using AiDesigner.Models;

namespace AiDesigner.Services;


public interface IPhotoAnalyzer
{

    Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default);
}
