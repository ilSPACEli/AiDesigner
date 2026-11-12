using AiDesigner.Models;
using AiDesigner.Repositories;

namespace AiDesigner.Services.Suggestions;

public class SuggestionService
{
    private readonly ICatalogRepository _repository;

    // Впроваджуємо репозиторій через DI
    public SuggestionService(ICatalogRepository repository)
    {
        _repository = repository;
    }

    // Метод стає асинхронним, бо репозиторій повертає Task
    public async Task<IEnumerable<CatalogItem>> GetSuggestionsAsync(
        /* Твої параметри з AnalysisRequestDto або моделі аналізу */
        CancellationToken cancellationToken = default)
    {
        // 1. Отримуємо весь каталог з репозиторію
        var allItems = await _repository.GetAllAsync(cancellationToken);

        // 2. TODO: Скопіюй сюди логіку фільтрації (правила R1-R5) 
        // з методу Select старого файлу Services/Catalog.cs
        
        var suggestedItems = allItems; // застосуй правила до allItems...

        return suggestedItems;
    }
}