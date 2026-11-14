using AiDesigner.Api.Dto;
using AiDesigner.Clients;

namespace AiDesigner.Tests.Clients;

// Підробка клієнта добору: відповідає так, як задав тест, і запам'ятовує запити.
internal sealed class FakeSuggestionClient(Func<AnalysisRequestDto, SuggestionClientResult> respond) : ISuggestionClient
{
    public List<AnalysisRequestDto> Requests { get; } = new();

    public Task<SuggestionClientResult> SuggestAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}
