using AiDesigner.Data;
using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Repositories;

public sealed class EfResultRepository : IResultRepository
{
    private readonly AppDbContext _db;

    public EfResultRepository(AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<SavedResult> AddAsync(SavedResult result, int evictOldest = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(evictOldest);

        var entity = new SavedResult
        {
            CreatedAt = result.CreatedAt,
            CatalogItemId = result.CatalogItemId,
            ImageData = result.ImageData,
            ThumbnailData = result.ThumbnailData
        };


        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (evictOldest > 0)
            {
                var oldestIds = await _db.SavedResults
                    .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                    .Select(r => r.Id)
                    .Take(evictOldest)
                    .ToListAsync(cancellationToken);

                await _db.SavedResults
                    .Where(r => oldestIds.Contains(r.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            _db.SavedResults.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            _db.Entry(entity).State = EntityState.Detached;
            throw;
        }

        return entity;
    }

    public Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default) =>
        ToSummariesAsync(
            _db.SavedResults.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            cancellationToken);

    public Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.SavedResults.AsNoTracking()
            .Include(r => r.CatalogItem)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rows = await _db.SavedResults
            .Where(r => r.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rows > 0;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _db.SavedResults.CountAsync(cancellationToken);

    public Task<IReadOnlyList<SavedResult>> GetOldestAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return ToSummariesAsync(
            _db.SavedResults.AsNoTracking()
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Take(count),
            cancellationToken);
    }

    // Список без стовпців ImageData і ThumbnailData: вони найважчі, а список віддає лише посилання.
    // Беремо Id, дату й предмет (його назва потрібна для ResultDto), а байти — через GetByIdAsync.
    private static async Task<IReadOnlyList<SavedResult>> ToSummariesAsync(
        IQueryable<SavedResult> ordered, CancellationToken cancellationToken)
    {
        var rows = await ordered
            .Select(r => new { r.Id, r.CreatedAt, r.CatalogItemId, r.CatalogItem })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SavedResult
            {
                Id = r.Id,
                CreatedAt = r.CreatedAt,
                CatalogItemId = r.CatalogItemId,
                CatalogItem = r.CatalogItem
            })
            .ToList();
    }
}