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

        var pendingIds = new List<string>();
        try
        {
            return Apply(db, items, pendingIds);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();

            // Повторюємо лише конфлікт одночасного старту: у БД з'явилися предмети, які ми намагалися додати
            // (їх устиг вставити інший екземпляр). Будь-яка інша помилка запису — пробрасуємо без повтору.
            if (pendingIds.Count == 0 || !db.CatalogItems.Any(i => pendingIds.Contains(i.Id)))
                throw;

            return Apply(db, items, new List<string>());
        }
    }

    // pendingIds — id предметів, які додаються (щоб розпізнати конфлікт одночасного старту).
    private static CatalogSyncResult Apply(AppDbContext db, IReadOnlyList<CatalogItem> items, List<string> pendingIds)
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
                pendingIds.Add(item.Id);
                added++;
            }
        }

        if (added + updated > 0)
            db.SaveChanges();

        var missingInJson = stored.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        return new CatalogSyncResult(added, updated, missingInJson);
    }
}
