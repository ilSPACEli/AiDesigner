using AiDesigner.Api.Dto;
using AiDesigner.Models;
using AiDesigner.Repositories;

namespace AiDesigner.Services.Suggestions;

public class SuggestionService
{
    private readonly ICatalogRepository _repository;

    public SuggestionService(ICatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CatalogItem>> GetSuggestionsAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        var allItems = await _repository.GetAllAsync(cancellationToken);

        var suggestedItems = allItems;

        return suggestedItems;
    }
}