using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Data;


public static class DatabaseInitializer
{
    public static void Initialize(AppDbContext db, string catalogPath, bool applyMigrations, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        if (applyMigrations)
            db.Database.Migrate();

        try
        {
            var result = CatalogSeeder.Sync(db, catalogPath);
            logger.LogInformation("Каталог синхронізовано з catalog.json: додано {Added}, оновлено {Updated}.",
                result.Added, result.Updated);

            if (result.MissingInJson.Count > 0)
                logger.LogWarning(
                    "У БД є предмети, яких немає в catalog.json: {Ids}. Їх не видалено — на них можуть посилатися збережені роботи.",
                    string.Join(", ", result.MissingInJson));
        }
        catch (CatalogException ex)
        {
            logger.LogError(ex, "Каталог не синхронізовано: {Reason}", ex.Message);
        }
    }
}
