using AiDesigner.Models;

namespace AiDesigner.Repositories;

public sealed class InMemoryResultRepository : IResultRepository
{
    private readonly object _sync = new();
    private readonly List<SavedResult> _items = new();
    private int _nextId = 1;

    public Task<SavedResult> AddAsync(SavedResult result, int evictOldest = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        // Уся перевірка — ДО змін: якщо вона не пройшла, сховище лишається як було.
        ArgumentOutOfRangeException.ThrowIfNegative(evictOldest);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            // Найстаріші вибираємо серед наявних, ще до додавання нової: вона не може бути витіснена.
            foreach (var old in Oldest(evictOldest))
                _items.Remove(old);

            // Id — init-only, тож присвоюємо його, створюючи копію (як це робить БД).
            var stored = new SavedResult
            {
                Id = _nextId++,
                CreatedAt = result.CreatedAt,
                CatalogItemId = result.CatalogItemId,
                CatalogItem = result.CatalogItem,
                ImageData = result.ImageData,
                ThumbnailData = result.ThumbnailData
            };
            _items.Add(stored);
            return Task.FromResult(stored);
        }
    }

    public Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            IReadOnlyList<SavedResult> list = _items
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult(_items.FirstOrDefault(r => r.Id == id));
    }

    public Task<byte[]?> GetImageAsync(int id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult(_items.FirstOrDefault(r => r.Id == id)?.ImageData);
    }

    public Task<byte[]?> GetThumbnailAsync(int id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult(_items.FirstOrDefault(r => r.Id == id)?.ThumbnailData);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult(_items.RemoveAll(r => r.Id == id) > 0);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult(_items.Count);
    }

    public Task<IReadOnlyList<SavedResult>> GetOldestAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
            return Task.FromResult<IReadOnlyList<SavedResult>>(Oldest(count));
    }

    // Викликати лише всередині lock.
    private List<SavedResult> Oldest(int count) => _items
        .OrderBy(r => r.CreatedAt)
        .ThenBy(r => r.Id)
        .Take(count)
        .ToList();
}
