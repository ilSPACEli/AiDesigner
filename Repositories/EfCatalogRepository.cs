using AiDesigner.Data;
using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Repositories;

// Каталог із PostgreSQL (таблиця CatalogItems).
public sealed class EfCatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _db;

    public EfCatalogRepository(AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // AsNoTracking: каталог лише читаємо, відстежувати зміни не потрібно.
    public async Task<IReadOnlyList<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.CatalogItems.AsNoTracking()
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

    public Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _db.CatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
}
