using AiDesigner.Models;

namespace AiDesigner.Repositories;

// Доступ до каталогу предметів (таблиця CatalogItems — робоча копія catalog.json, #48).
// Лише читання: каталог змінюється через catalog.json і синхронізацію під час старту.
public interface ICatalogRepository
{
    // Усі предмети, упорядковані за Id (стабільний порядок для правил добору).
    Task<IReadOnlyList<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
