using System.Net.Http.Json;
using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public class SuggestionClient : ISuggestionClient
{ 
    private readonly HttpClient _httpClient;

    public SuggestionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<SuggestionDto>> GetSuggestionsAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/suggestions", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<SuggestionDto>();
        }

        var suggestions = await response.Content.ReadFromJsonAsync<IEnumerable<SuggestionDto>>(cancellationToken: cancellationToken);
        return suggestions ?? Array.Empty<SuggestionDto>();
    }
}