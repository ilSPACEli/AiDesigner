using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;
public interface ISuggestionClient
{
    Task<IEnumerable<SuggestionDto>> GetSuggestionsAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default);
}