using System.Net;
using System.Net.Http.Json;
using AiDesigner.Api.Dto;

namespace AiDesigner.Clients;

public sealed class SuggestionClient : ISuggestionClient
{
    public const string SuggestionsPath = "api/suggestions";

    private readonly HttpClient _http;

    public SuggestionClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }


    public async Task<SuggestionClientResult> SuggestAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _http.PostAsJsonAsync(SuggestionsPath, request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            return SuggestionClientResult.NoSuitableItems();

        if (!response.IsSuccessStatusCode)
            return SuggestionClientResult.Unavailable();

        var suggestions = await response.Content.ReadFromJsonAsync<List<SuggestionDto>>(cancellationToken);
        return suggestions is { Count: > 0 }
            ? SuggestionClientResult.Success(suggestions)
            : SuggestionClientResult.Unavailable();
    }
}
