using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Data;

public sealed record CatalogSyncResult(int Added, int Updated, IReadOnlyList<string> MissingInJson);

public static class CatalogSeeder
{
    // Повертає кількість доданих предметів: 0, якщо таблиця вже заповнена
    public static CatalogSyncResult Sync(AppDbContext db, string catalogPath) =>
        new(Seed(db, catalogPath), 0, Array.Empty<string>());

    public static int Seed(AppDbContext db, string catalogPath)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (db.CatalogItems.Any())
            return 0;

        var items = CatalogLoader.LoadFromFile(catalogPath);
        return Insert(db, items);
    }

    // Вставка всіх предметів однією транзакцією (SaveChanges): або всі, або жоден.
    internal static int Insert(AppDbContext db, IReadOnlyList<CatalogItem> items)
    {
        db.CatalogItems.AddRange(items);
        try
        {
            db.SaveChanges();
            return items.Count;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (db.CatalogItems.Any())
                return 0;
            throw;
        }
    }
}
