using AiDesigner.Models; // Переконайся, що простір імен збігається з твоїм

namespace AiDesigner.Repositories;

public interface ICatalogRepository
{
    Task<IEnumerable<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}