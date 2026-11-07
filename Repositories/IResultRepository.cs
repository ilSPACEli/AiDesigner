using AiDesigner.Models;

namespace AiDesigner.Repositories;

public interface IResultRepository
{

    Task<SavedResult> AddAsync(SavedResult result, int evictOldest = 0, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedResult>> GetOldestAsync(int count, CancellationToken cancellationToken = default);
}