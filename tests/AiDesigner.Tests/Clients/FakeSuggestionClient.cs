   using AiDesigner.Clients;

   namespace AiDesigner.Tests.Clients;

   internal sealed class FakeSuggestionClient : ISuggestionClient
    {
        private sealed class FakeSuggestionClient(Func<IEnumerable<SuggestionDto>> getSuggestions) : ISuggestionClient
        {
            public Task<IEnumerable<SuggestionDto>> GetSuggestionsAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(getSuggestions());
        }
    }