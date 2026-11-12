using AiDesigner.Models;

namespace AiDesigner.Repositories;

// Каталог у пам'яті — для тестів і роботи без БД.
public sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly IReadOnlyList<CatalogItem> _items;

    public InMemoryCatalogRepository(IEnumerable<CatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
    }

    public Task<IReadOnlyList<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_items);

    public Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
}
