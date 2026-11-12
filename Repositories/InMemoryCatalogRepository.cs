using AiDesigner.Models;

namespace AiDesigner.Repositories;

public class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly List<CatalogItem> _items;

    public InMemoryCatalogRepository(IEnumerable<CatalogItem> items)
    {
        _items = items.ToList();
    }

    public Task<IEnumerable<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<CatalogItem>>(_items);
    }

    public Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(item);
    }
}