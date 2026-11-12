using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public interface ISuggestionClient
{
    Task<SuggestionClientResult> SuggestAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default);
}
