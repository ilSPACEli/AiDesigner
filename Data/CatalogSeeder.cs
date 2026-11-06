using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Data;

public sealed record CatalogSyncResult(int Added, int Updated, IReadOnlyList<string> MissingInJson);


public static class CatalogSeeder
{
    public static CatalogSyncResult Sync(AppDbContext db, string catalogPath)
    {
        ArgumentNullException.ThrowIfNull(db);

        var items = CatalogLoader.LoadFromFile(catalogPath);

        try
        {
            return Apply(db, items);
        }
        catch (DbUpdateException)
        {

            db.ChangeTracker.Clear();
            return Apply(db, items);
        }
    }

    private static CatalogSyncResult Apply(AppDbContext db, IReadOnlyList<CatalogItem> items)
    {
        var stored = db.CatalogItems.ToDictionary(i => i.Id);
        int added = 0, updated = 0;

        foreach (var item in items)
        {
            if (stored.Remove(item.Id, out var existing))
            {
                var entry = db.Entry(existing);
                entry.CurrentValues.SetValues(item);
                if (entry.State == EntityState.Modified)
                    updated++;
            }
            else
            {
                db.CatalogItems.Add(item);
                added++;
            }
        }

        if (added + updated > 0)
            db.SaveChanges();

        var missingInJson = stored.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        return new CatalogSyncResult(added, updated, missingInJson);
    }
}
