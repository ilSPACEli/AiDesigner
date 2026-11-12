using System.Data.Common;
using AiDesigner.Models;
using AiDesigner.Repositories;

namespace AiDesigner.Services.Suggestions;

// Добір пропозицій (UC-03): предмети — з репозиторію (БД), правила R1–R5 — SuggestionRules.
public sealed class SuggestionService
{
    // Скільки пропозицій повертає API (FR-06: 3–4).
    public const int SuggestionCount = 4;

    private readonly ICatalogRepository _repository;
    private readonly ILogger<SuggestionService> _logger;

    public SuggestionService(ICatalogRepository repository, ILogger<SuggestionService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    public async Task<SuggestionOutcome> SuggestAsync(AnalysisResult analysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        IReadOnlyList<CatalogItem> items;
        try
        {
            items = await _repository.GetAllAsync(cancellationToken);
        }
        catch (DbException ex)
        {
            // БД не відповідає — це «каталог недоступний», а не збій програми.
            _logger.LogError(ex, "Каталог недоступний: не вдалося прочитати CatalogItems.");
            return SuggestionOutcome.CatalogUnavailable();
        }

        // Порожня таблиця — каталог не синхронізовано (зламаний catalog.json, #48): теж «недоступний».
        if (items.Count == 0)
            return SuggestionOutcome.CatalogUnavailable();

        var suggestions = new SuggestionRules(items).Select(analysis, SuggestionCount);
        return suggestions.Count == 0
            ? SuggestionOutcome.NoSuitableItems()
            : SuggestionOutcome.Ok(suggestions);
    }
}
