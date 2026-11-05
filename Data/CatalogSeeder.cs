namespace AiDesigner.Data;

public static class CatalogSeeder
{
    public static int Seed(AppDbContext db, string catalogPath)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (db.CatalogItems.Any())
            return 0;

        var items = CatalogLoader.LoadFromFile(catalogPath);

        db.CatalogItems.AddRange(items);
        db.SaveChanges();
        return items.Count;
    }
}
