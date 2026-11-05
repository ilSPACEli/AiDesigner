using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Data;

// Початкове заповнення таблиці CatalogItems із wwwroot/data/catalog.json
// Працює лише з порожньою таблицею, тож повторний запуск застосунку нічого не дублює
public static class CatalogSeeder
{
    // Повертає кількість доданих предметів: 0, якщо таблиця вже заповнена
    // Якщо catalog.json відсутній або містить помилки — CatalogException від CatalogLoader.
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
