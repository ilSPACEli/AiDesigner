using AiDesigner.Data;
using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Repositories;

public class EfCatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _context;

    public EfCatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CatalogItems.ToListAsync(cancellationToken);
    }

    public async Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.CatalogItems
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }
}